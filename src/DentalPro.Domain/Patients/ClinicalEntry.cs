namespace DentalPro.Domain.Patients;

public enum ClinicalEntryType
{
    Consultation,
    Treatment,
    Observation,
    FollowUp,
    ExternalRecord,
    Other
}

/// <summary>An immutable clinical encounter. Amendments are represented by new versions.</summary>
public sealed class ClinicalEntry
{
    private readonly Guid[] _treatmentIds;

    private ClinicalEntry(Guid id, Guid medicalRecordId, Guid dentistId, Guid? appointmentId,
        ClinicalEntryType type, DateTimeOffset occurredAt, string? diagnosis, string notes,
        IEnumerable<Guid> treatmentIds, DateTimeOffset createdAt)
    {
        Id = id;
        MedicalRecordId = medicalRecordId;
        DentistId = dentistId;
        AppointmentId = appointmentId;
        Type = type;
        OccurredAt = occurredAt.ToUniversalTime();
        Diagnosis = CleanOptional(diagnosis);
        Notes = EnsureRequired(notes, nameof(notes));
        _treatmentIds = treatmentIds.ToArray();
        CreatedAt = UpdatedAt = createdAt.ToUniversalTime();
        Validate();
    }

    public Guid Id { get; }
    public Guid MedicalRecordId { get; }
    public Guid DentistId { get; }
    public Guid? AppointmentId { get; }
    public ClinicalEntryType Type { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? Diagnosis { get; }
    public string Notes { get; }
    public IReadOnlyList<Guid> TreatmentIds => Array.AsReadOnly(_treatmentIds);
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static ClinicalEntry Create(Guid medicalRecordId, Guid dentistId, Guid? appointmentId,
        ClinicalEntryType type, DateTimeOffset occurredAt, string? diagnosis, string notes,
        IEnumerable<Guid>? treatmentIds = null, DateTimeOffset? createdAt = null)
    {
        var ids = (treatmentIds ?? []).ToArray();
        return new ClinicalEntry(Guid.NewGuid(), medicalRecordId, dentistId, appointmentId, type,
            occurredAt, diagnosis, notes, ids, createdAt ?? DateTimeOffset.UtcNow);
    }

    private void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("Clinical entry ID is required.", nameof(Id));
        if (MedicalRecordId == Guid.Empty) throw new ArgumentException("Medical record is required.", nameof(MedicalRecordId));
        if (DentistId == Guid.Empty) throw new ArgumentException("Responsible dentist is required.", nameof(DentistId));
        if (AppointmentId == Guid.Empty) throw new ArgumentException("Appointment ID cannot be empty.", nameof(AppointmentId));
        if (!Enum.IsDefined(Type)) throw new ArgumentOutOfRangeException(nameof(Type));
        if (Notes.Length > 20_000) throw new ArgumentOutOfRangeException(nameof(Notes));
        if (Diagnosis?.Length > 2_000) throw new ArgumentOutOfRangeException(nameof(Diagnosis));
        if (_treatmentIds.Any(id => id == Guid.Empty) || _treatmentIds.Distinct().Count() != _treatmentIds.Length)
            throw new ArgumentException("Treatment IDs must be non-empty and unique.", nameof(TreatmentIds));
    }

    private static string EnsureRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        return value.Trim();
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Immutable complete snapshot created with each clinical entry version.</summary>
public sealed class ClinicalEntryVersion
{
    private ClinicalEntryVersion(Guid id, Guid clinicalEntryId, int versionNumber, Guid medicalRecordId,
        Guid dentistId, Guid? appointmentId, ClinicalEntryType type, DateTimeOffset occurredAt,
        string? diagnosis, string notes, IEnumerable<Guid> treatmentIds, DateTimeOffset createdAt,
        Guid createdBy, string? changeReason)
    {
        Id = id;
        ClinicalEntryId = clinicalEntryId;
        VersionNumber = versionNumber;
        MedicalRecordId = medicalRecordId;
        DentistId = dentistId;
        AppointmentId = appointmentId;
        Type = type;
        OccurredAt = occurredAt.ToUniversalTime();
        Diagnosis = diagnosis;
        Notes = notes;
        TreatmentIds = Array.AsReadOnly(treatmentIds.ToArray());
        CreatedAt = createdAt.ToUniversalTime();
        CreatedBy = createdBy;
        ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim();
    }

    public Guid Id { get; }
    public Guid ClinicalEntryId { get; }
    public int VersionNumber { get; }
    public Guid MedicalRecordId { get; }
    public Guid DentistId { get; }
    public Guid? AppointmentId { get; }
    public ClinicalEntryType Type { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? Diagnosis { get; }
    public string Notes { get; }
    public IReadOnlyList<Guid> TreatmentIds { get; }
    public DateTimeOffset CreatedAt { get; }
    public Guid CreatedBy { get; }
    public string? ChangeReason { get; }

    public static ClinicalEntryVersion Initial(ClinicalEntry entry, Guid createdBy,
        string? changeReason = null, DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (createdBy == Guid.Empty) throw new ArgumentException("Version author is required.", nameof(createdBy));
        return new ClinicalEntryVersion(Guid.NewGuid(), entry.Id, 1, entry.MedicalRecordId,
            entry.DentistId, entry.AppointmentId, entry.Type, entry.OccurredAt, entry.Diagnosis,
            entry.Notes, entry.TreatmentIds, createdAt ?? DateTimeOffset.UtcNow, createdBy, changeReason);
    }
}
