using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public sealed class RescheduleAppointment(
    IAppointmentRepository repository,
    IAppointmentParticipantQuery participants,
    IClinicScheduleQuery clinicSchedule,
    IAppointmentUnitOfWork unitOfWork,
    IAuditEventWriter auditWriter,
    IEffectivePermissionQuery permissions)
{
    public Task<Appointment> ExecuteAsync(
        Guid actorId,
        RescheduleAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        AppointmentScopeResolver.EnsureActor(actorId);
        return unitOfWork.ExecuteAsync(async ct =>
        {
            var original = await repository.GetByIdForUpdateAsync(command.AppointmentId, ct)
                ?? throw new AppointmentNotFoundException(command.AppointmentId);
            var scope = new AppointmentScopeResolver(permissions);
            await scope.EnsurePermissionAsync(actorId, AppointmentPermissionCodes.Update, ct);
            await scope.EnsureCanManageDentistAsync(actorId, original.DentistId, ct);
            await scope.EnsureCanManageDentistAsync(actorId, command.DentistId, ct);
            await GetAppointmentAvailability.ValidatePatientAsync(original.PatientId, participants, ct);
            await GetAppointmentAvailability.ValidateDentistAsync(command.DentistId, participants, ct);

            var settings = await clinicSchedule.GetSettingsAsync(ct);
            var start = ClinicScheduleTime.ResolveLocalStart(command.StartDateTime, settings);
            var duration = command.DurationMinutes ?? original.DurationMinutes;
            var end = ClinicScheduleTime.CalculateEnd(start, duration);
            _ = Appointment.Create(original.PatientId, command.DentistId, start, duration, command.Notes);
            await CreateAppointment.EnsureWithinClinicHoursAsync(clinicSchedule, start, end, ct);
            await CreateAppointment.EnsureAvailableAsync(
                repository, original.PatientId, command.DentistId, start, end, original.Id, ct);

            Appointment replacement;
            try
            {
                replacement = original.Reschedule(command.DentistId, start, duration, command.Notes);
            }
            catch (InvalidOperationException exception)
            {
                throw new AppointmentInvalidStateTransitionException(exception.Message);
            }

            await repository.UpdateAsync(original, ct);
            repository.Add(replacement);
            await auditWriter.AddAsync(new AppointmentAuditEntry(
                actorId,
                AppointmentAuditActions.Reschedule,
                original.Id,
                replacement.Id,
                DateTimeOffset.UtcNow), ct);
            return replacement;
        }, cancellationToken);
    }
}

public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    Guid DentistId,
    DateTimeOffset StartDateTime,
    int? DurationMinutes = null,
    string? Notes = null)
{
    public RescheduleAppointmentCommand(Guid appointmentId, Guid dentistId, DateTimeOffset startDateTime,
        DateTimeOffset endDateTime, string? notes = null)
        : this(appointmentId, dentistId, startDateTime,
            CreateAppointmentCommand.ExactMinutesCompat(startDateTime, endDateTime), notes) { }
}
