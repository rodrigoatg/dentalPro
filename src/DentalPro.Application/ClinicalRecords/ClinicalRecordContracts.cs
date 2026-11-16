using DentalPro.Application.AccessControl;
using DentalPro.Domain.Patients;
using DentalPro.Domain.Treatments;

namespace DentalPro.Application.ClinicalRecords;

public static class ClinicalPermissionCodes
{
    public const string RecordsView = PermissionCatalog.ClinicalRecordsView;
    public const string RecordsViewAll = PermissionCatalog.ClinicalRecordsViewAll;
    public const string EntriesCreate = PermissionCatalog.ClinicalEntriesCreate;
    public const string TreatmentsCreate = PermissionCatalog.TreatmentsCreate;
}

public sealed record ClinicalTreatmentCommand(TreatmentType Type, string Description, TreatmentStatus Status,
    IReadOnlyList<int> ToothNumbers, TreatmentPriority Priority = TreatmentPriority.Medium, decimal? Cost = null,
    IReadOnlyList<int>? CompletedToothNumbers = null);

public sealed record CreateClinicalEntryCommand(Guid PatientId, Guid? AppointmentId, ClinicalEntryType Type,
    DateTimeOffset OccurredAt, string? Diagnosis, string Notes, IReadOnlyList<ClinicalTreatmentCommand> Treatments);

public sealed record ClinicalRecordDetails(Guid Id, Guid PatientId, string PatientFirstName, string PatientLastName, long MedicalRecordNumber,
    DateTimeOffset StartedAt, DateTimeOffset? LastClinicalActivityAt, IReadOnlyList<ClinicalEntryDetails> Entries);

public sealed record ClinicalEntryDetails(Guid Id, ClinicalEntryType Type, Guid DentistId,
    string DentistFirstName, string DentistLastName, Guid? AppointmentId, DateTimeOffset OccurredAt,
    string? Diagnosis, string Notes, IReadOnlyList<ClinicalEntryVersionDetails> Versions,
    IReadOnlyList<ClinicalTreatmentDetails> Treatments);

public sealed record ClinicalEntryVersionDetails(int VersionNumber, Guid DentistId, Guid? AppointmentId,
    ClinicalEntryType Type, DateTimeOffset OccurredAt, string? Diagnosis, string Notes,
    IReadOnlyList<Guid> TreatmentIds, DateTimeOffset CreatedAt, Guid CreatedBy,
    string CreatedByFirstName, string CreatedByLastName, string? ChangeReason);

public sealed record ClinicalTreatmentDetails(Guid Id, Guid DentistId, string DentistFirstName,
    string DentistLastName, string Type, DateTimeOffset OccurredAt, string Description, string Priority,
    string Status, decimal? Cost, IReadOnlyList<ClinicalTreatmentToothDetails> Teeth);

public sealed record ClinicalTreatmentToothDetails(int ToothNumber, string Status,
    DateTimeOffset? CompletedAt, string? Notes, string? ResultingToothState);
