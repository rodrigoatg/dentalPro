using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public sealed class CompleteAppointment(
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
            await new AppointmentScopeResolver(permissions).EnsureOwnsAsync(actorId, appointment, true, ct);
            AppointmentStateChanges.Change(appointment, appointment.Complete);
            await auditWriter.AddAsync(new AppointmentAuditEntry(
                actorId, AppointmentAuditActions.Complete, appointment.Id, null, DateTimeOffset.UtcNow), ct);
            return appointment;
        }, cancellationToken);
}
