using DentalPro.Application.Appointments;
using DentalPro.Application.ClinicalRecords;
using DentalPro.Infrastructure.Persistence.Models;

namespace DentalPro.Infrastructure.Persistence;

public sealed class AuditEventWriter(DentalProDbContext dbContext) : IAuditEventWriter, IClinicalAuditWriter
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

    public Task AddAsync(ClinicalAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid(), ActorId = entry.ActorId, EntityType = entry.EntityType,
            EntityId = entry.EntityId, Action = entry.Action, OccurredAt = entry.OccurredAt.ToUniversalTime(),
            RelatedEntityId = entry.RelatedEntityId
        });
        return Task.CompletedTask;
    }
}
