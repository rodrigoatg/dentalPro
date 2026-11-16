namespace DentalPro.Api.ClinicalRecords;

public sealed record ClinicalRecordResponse(Guid Id, Guid PatientId, string PatientFirstName, string PatientLastName, long MedicalRecordNumber,
    DateTimeOffset StartedAt, DateTimeOffset? LastClinicalActivityAt, IReadOnlyList<ClinicalEntryResponse> Entries);
public sealed record ClinicalEntryResponse(Guid Id, string Type, Guid DentistId, string DentistFirstName,
    string DentistLastName, Guid? AppointmentId, DateTimeOffset OccurredAt, string? Diagnosis, string Notes,
    IReadOnlyList<ClinicalEntryVersionResponse> Versions, IReadOnlyList<ClinicalTreatmentResponse> Treatments);
public sealed record ClinicalEntryVersionResponse(int VersionNumber, Guid DentistId, Guid? AppointmentId,
    string Type, DateTimeOffset OccurredAt, string? Diagnosis, string Notes, IReadOnlyList<Guid> TreatmentIds,
    DateTimeOffset CreatedAt, Guid CreatedBy, string CreatedByFirstName, string CreatedByLastName,
    string? ChangeReason);
public sealed record ClinicalTreatmentResponse(Guid Id, Guid DentistId, string DentistFirstName,
    string DentistLastName, string Type, DateTimeOffset OccurredAt, string Description, string Priority,
    string Status, decimal? Cost, IReadOnlyList<ClinicalTreatmentToothResponse> Teeth);
public sealed record ClinicalTreatmentToothResponse(int ToothNumber, string Status,
    DateTimeOffset? CompletedAt, string? Notes, string? ResultingToothState);
