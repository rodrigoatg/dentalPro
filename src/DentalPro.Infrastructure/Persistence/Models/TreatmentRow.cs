using DentalPro.Domain.Treatments;

namespace DentalPro.Infrastructure.Persistence.Models;

public sealed class TreatmentRow
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public Guid DentistId { get; set; }
    public TreatmentType Type { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public TreatmentPriority Priority { get; set; }
    public TreatmentStatus Status { get; set; }
    public bool IsFinalized { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public decimal? Cost { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TreatmentToothRow> Teeth { get; set; } = [];
    public List<ClinicalEntryTreatmentRow> ClinicalEntries { get; set; } = [];
}

public sealed class TreatmentToothRow
{
    public Guid TreatmentId { get; set; }
    public int ToothNumber { get; set; }
    public TreatmentToothStatus Status { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public string? ResultingToothState { get; set; }
    public TreatmentRow Treatment { get; set; } = null!;
}
