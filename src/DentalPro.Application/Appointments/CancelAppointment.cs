using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public sealed class CancelAppointment(
    IAppointmentRepository repository,
    IAppointmentUnitOfWork unitOfWork,
    IAuditEventWriter auditWriter,
    IEffectivePermissionQuery permissions)
{
    public Task<Appointment> ExecuteAsync(Guid actorId, Guid appointmentId, CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            AppointmentScopeResolver.EnsureActor(actorId);
            var appointment = await repository.GetByIdForUpdateAsync(appointmentId, ct)
                ?? throw new AppointmentNotFoundException(appointmentId);
            var scope = new AppointmentScopeResolver(permissions);
            await scope.EnsurePermissionAsync(actorId, AppointmentPermissionCodes.Cancel, ct);
            await scope.EnsureCanManageDentistAsync(actorId, appointment.DentistId, ct);
            AppointmentStateChanges.Change(appointment, appointment.Cancel);
            await repository.UpdateAsync(appointment, ct);
            await auditWriter.AddAsync(new AppointmentAuditEntry(
                actorId, AppointmentAuditActions.Cancel, appointment.Id, null, DateTimeOffset.UtcNow), ct);
            return appointment;
        }, cancellationToken);
}
