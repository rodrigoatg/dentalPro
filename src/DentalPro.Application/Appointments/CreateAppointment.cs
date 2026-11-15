using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public sealed class CreateAppointment(
    IAppointmentRepository repository,
    IAppointmentParticipantQuery participants,
    IClinicScheduleQuery clinicSchedule,
    IAppointmentUnitOfWork unitOfWork,
    IAuditEventWriter auditWriter,
    IEffectivePermissionQuery permissions)
{
    public Task<Appointment> ExecuteAsync(
        Guid actorId,
        CreateAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        AppointmentScopeResolver.EnsureActor(actorId);
        return unitOfWork.ExecuteAsync(async ct =>
        {
            var scope = new AppointmentScopeResolver(permissions);
            await scope.EnsurePermissionAsync(actorId, AppointmentPermissionCodes.Create, ct);
            await scope.EnsureCanManageDentistAsync(actorId, command.DentistId, ct);
            await GetAppointmentAvailability.ValidatePatientAsync(command.PatientId, participants, ct);
            await GetAppointmentAvailability.ValidateDentistAsync(command.DentistId, participants, ct);

            var settings = await clinicSchedule.GetSettingsAsync(ct);
            var start = ClinicScheduleTime.ResolveLocalStart(command.StartDateTime, settings);
            var duration = command.DurationMinutes ?? settings.DefaultAppointmentDurationMinutes;
            var end = ClinicScheduleTime.CalculateEnd(start, duration);
            var appointment = Appointment.Create(
                command.PatientId, command.DentistId, start, duration, command.Notes);
            await EnsureWithinClinicHoursAsync(clinicSchedule, start, end, ct);
            await EnsureAvailableAsync(repository, command.PatientId, command.DentistId, start, end, null, ct);

            repository.Add(appointment);
            await auditWriter.AddAsync(
                new AppointmentAuditEntry(actorId, AppointmentAuditActions.Create, appointment.Id, null, DateTimeOffset.UtcNow), ct);
            return appointment;
        }, cancellationToken);
    }

    internal static async Task EnsureWithinClinicHoursAsync(
        IClinicScheduleQuery clinicSchedule,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var openings = await clinicSchedule.GetOpenIntervalsAsync(start, end, cancellationToken);
        if (!openings.Any(opening =>
                opening.Start.ToUniversalTime() <= start && opening.End.ToUniversalTime() >= end))
        {
            throw new AppointmentOutsideWorkingHoursException();
        }
    }

    internal static async Task EnsureAvailableAsync(
        IAppointmentRepository repository,
        Guid patientId,
        Guid dentistId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludingAppointmentId,
        CancellationToken cancellationToken)
    {
        var blocking = await repository.GetBlockingAsync(
            dentistId, patientId, start, end, excludingAppointmentId, cancellationToken);
        if (blocking.Any(appointment =>
                appointment.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Completed
                && appointment.StartDateTime < end && appointment.EndDateTime > start))
        {
            throw new AppointmentAvailabilityConflictException();
        }
    }
}

public sealed record CreateAppointmentCommand(
    Guid PatientId,
    Guid DentistId,
    DateTimeOffset StartDateTime,
    int? DurationMinutes = null,
    string? Notes = null)
{
    public CreateAppointmentCommand(Guid patientId, Guid dentistId, DateTimeOffset startDateTime,
        DateTimeOffset endDateTime, string? notes = null)
        : this(patientId, dentistId, startDateTime,
            ExactMinutesCompat(startDateTime, endDateTime), notes) { }

    internal static int ExactMinutesCompat(DateTimeOffset start, DateTimeOffset end)
    {
        var minutes = (end.ToUniversalTime() - start.ToUniversalTime()).TotalMinutes;
        if (minutes <= 0 || minutes != Math.Truncate(minutes) || minutes > int.MaxValue)
            throw new ArgumentException("Appointment interval must have an exact positive whole-minute duration.", nameof(end));
        return (int)minutes;
    }
}
