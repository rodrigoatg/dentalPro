namespace DentalPro.Application.ClinicalRecords;

public sealed record ClinicalAuditEntry(Guid ActorId, string EntityType, Guid EntityId,
    string Action, DateTimeOffset OccurredAt, Guid? RelatedEntityId = null);

public interface IClinicalAuditWriter
{
    Task AddAsync(ClinicalAuditEntry entry, CancellationToken cancellationToken = default);
}
