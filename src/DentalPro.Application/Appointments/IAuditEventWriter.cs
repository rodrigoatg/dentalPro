namespace DentalPro.Application.Appointments;

public interface IAuditEventWriter
{
    Task AddAsync(AppointmentAuditEntry entry, CancellationToken cancellationToken = default);
}
