using DentalPro.Application.Appointments;
using DentalPro.Infrastructure.Persistence.Models;

namespace DentalPro.Infrastructure.Persistence;

public sealed class AuditEventWriter(DentalProDbContext dbContext) : IAuditEventWriter
{
    public Task AddAsync(AppointmentAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        dbContext.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid(),
            ActorId = entry.ActorId,
            EntityType = "Appointment",
            EntityId = entry.AppointmentId,
            Action = entry.Action,
            OccurredAt = entry.OccurredAt.ToUniversalTime(),
            RelatedEntityId = entry.RelatedAppointmentId
        });

        return Task.CompletedTask;
    }
}
