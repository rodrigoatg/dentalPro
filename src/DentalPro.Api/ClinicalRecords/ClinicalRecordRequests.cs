namespace DentalPro.Api.ClinicalRecords;

public sealed record CreateClinicalEntryRequest(
    string Type,
    DateTimeOffset OccurredAt,
    string Notes,
    string? Diagnosis = null,
    Guid? AppointmentId = null,
    IReadOnlyList<CreateClinicalTreatmentRequest?>? Treatments = null);

public sealed record CreateClinicalTreatmentRequest(
    string Type,
    string Description,
    string Status = "Planned",
    IReadOnlyList<int>? ToothNumbers = null,
    string Priority = "Medium",
    decimal? Cost = null,
    IReadOnlyList<int>? CompletedToothNumbers = null);
