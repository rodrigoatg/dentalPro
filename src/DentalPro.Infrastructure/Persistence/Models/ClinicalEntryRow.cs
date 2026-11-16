using DentalPro.Domain.Patients;

namespace DentalPro.Infrastructure.Persistence.Models;

public sealed class ClinicalEntryRow
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public Guid DentistId { get; set; }
    public Guid? AppointmentId { get; set; }
    public ClinicalEntryType Type { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Diagnosis { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ClinicalEntryVersionRow> Versions { get; set; } = [];
    public List<ClinicalEntryTreatmentRow> Treatments { get; set; } = [];
}

public sealed class ClinicalEntryVersionRow
{
    public Guid Id { get; set; }
    public Guid ClinicalEntryId { get; set; }
    public int VersionNumber { get; set; }
    public Guid MedicalRecordId { get; set; }
    public Guid DentistId { get; set; }
    public Guid? AppointmentId { get; set; }
    public ClinicalEntryType Type { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Diagnosis { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public long CreatedTransactionId { get; set; }
    public string? ChangeReason { get; set; }
    public ClinicalEntryRow ClinicalEntry { get; set; } = null!;
    public List<ClinicalEntryVersionTreatmentRow> Treatments { get; set; } = [];
}

public sealed class ClinicalEntryTreatmentRow
{
    public Guid ClinicalEntryId { get; set; }
    public Guid TreatmentId { get; set; }
    public ClinicalEntryRow ClinicalEntry { get; set; } = null!;
    public TreatmentRow Treatment { get; set; } = null!;
}

public sealed class ClinicalEntryVersionTreatmentRow
{
    public Guid ClinicalEntryVersionId { get; set; }
    public Guid TreatmentId { get; set; }
    public ClinicalEntryVersionRow ClinicalEntryVersion { get; set; } = null!;
    public TreatmentRow Treatment { get; set; } = null!;
}
