using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public sealed class GetAppointmentAvailability(
    IAppointmentRepository repository,
    IAppointmentParticipantQuery participants,
    IClinicScheduleQuery clinicSchedule,
    IEffectivePermissionQuery permissions)
{
    public async Task<AppointmentAvailability> ExecuteAsync(
        Guid actorId,
        AppointmentAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ListAppointments.ValidateRange(query.From, query.To);
        await new AppointmentScopeResolver(permissions)
            .EnsureCanManageDentistAsync(actorId, query.DentistId, cancellationToken);
        await ValidateDentistAsync(query.DentistId, cancellationToken);
        if (query.PatientId is { } patientId)
        {
            await ValidatePatientAsync(patientId, cancellationToken);
        }

        var from = query.From.ToUniversalTime();
        var to = query.To.ToUniversalTime();
        var openings = await clinicSchedule.GetOpenIntervalsAsync(from, to, cancellationToken);
        var busy = await repository.GetBlockingAsync(
            query.DentistId, query.PatientId, from, to, cancellationToken: cancellationToken);
        return new AppointmentAvailability(SubtractBusyIntervals(openings, busy, from, to));
    }

    internal static async Task ValidatePatientAsync(
        Guid patientId,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var status = await participants.GetPatientStatusAsync(patientId, cancellationToken);
        if (status != AppointmentPatientStatus.Active)
        {
            throw new AppointmentPatientUnavailableException(patientId, status);
        }
    }

    internal static async Task ValidateDentistAsync(
        Guid dentistId,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var status = await participants.GetDentistStatusAsync(dentistId, cancellationToken);
        if (status != AppointmentDentistStatus.ActiveDentist)
        {
            throw new AppointmentDentistUnavailableException(dentistId, status);
        }
    }

    private Task ValidatePatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        ValidatePatientAsync(patientId, participants, cancellationToken);

    private Task ValidateDentistAsync(Guid dentistId, CancellationToken cancellationToken) =>
        ValidateDentistAsync(dentistId, participants, cancellationToken);

    private static IReadOnlyList<AppointmentInterval> SubtractBusyIntervals(
        IReadOnlyList<AppointmentInterval> openings,
        IReadOnlyList<Appointment> appointments,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var result = new List<AppointmentInterval>();
        foreach (var opening in openings)
        {
            var cursor = opening.Start.ToUniversalTime() > from ? opening.Start.ToUniversalTime() : from;
            var openingEnd = opening.End.ToUniversalTime() < to ? opening.End.ToUniversalTime() : to;
            if (cursor >= openingEnd)
            {
                continue;
            }

            foreach (var busy in appointments
                         .Where(item => IsBlocking(item.Status)
                             && item.StartDateTime.ToUniversalTime() < openingEnd
                             && item.EndDateTime.ToUniversalTime() > cursor)
                         .OrderBy(item => item.StartDateTime))
            {
                var busyStart = busy.StartDateTime.ToUniversalTime();
                var busyEnd = busy.EndDateTime.ToUniversalTime();
                if (busyStart > cursor)
                {
                    result.Add(new AppointmentInterval(cursor, busyStart < openingEnd ? busyStart : openingEnd));
                }

                if (busyEnd > cursor)
                {
                    cursor = busyEnd;
                }

                if (cursor >= openingEnd)
                {
                    break;
                }
            }

            if (cursor < openingEnd)
            {
                result.Add(new AppointmentInterval(cursor, openingEnd));
            }
        }

        return result;
    }

    private static bool IsBlocking(AppointmentStatus status) =>
        status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Completed;
}
