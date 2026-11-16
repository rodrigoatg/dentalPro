namespace DentalPro.Domain.Patients;

public sealed class MedicalRecord
{
    private MedicalRecord(Guid id, Guid patientId, long medicalRecordNumber, DateTimeOffset startedAt,
        DateTimeOffset? lastClinicalActivityAt, DateTimeOffset createdAt, DateTimeOffset updatedAt,
        Odontogram odontogram)
    {
        Id = id;
        PatientId = patientId;
        MedicalRecordNumber = medicalRecordNumber;
        StartedAt = startedAt.ToUniversalTime();
        LastClinicalActivityAt = lastClinicalActivityAt?.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = updatedAt.ToUniversalTime();
        Odontogram = odontogram;
    }

    private MedicalRecord(Guid id, Guid patientId, DateTimeOffset createdAt)
    {
        Id = id;
        PatientId = patientId;
        StartedAt = createdAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Odontogram = Odontogram.Create(id, createdAt);
    }

    public Guid Id { get; }
    public Guid PatientId { get; }
    public long? MedicalRecordNumber { get; private set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? LastClinicalActivityAt { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Odontogram Odontogram { get; }

    internal static MedicalRecord Create(Guid patientId, DateTimeOffset createdAt)
    {
        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient is required.", nameof(patientId));
        }

        return new MedicalRecord(Guid.NewGuid(), patientId, createdAt);
    }

    public static MedicalRecord Rehydrate(Guid id, Guid patientId, long medicalRecordNumber,
        DateTimeOffset startedAt, DateTimeOffset? lastClinicalActivityAt, DateTimeOffset createdAt,
        DateTimeOffset updatedAt, Odontogram odontogram)
    {
        ArgumentNullException.ThrowIfNull(odontogram);
        if (id == Guid.Empty || patientId == Guid.Empty || medicalRecordNumber <= 0 ||
            odontogram.MedicalRecordId != id)
            throw new ArgumentException("Persisted medical record data is invalid.");
        return new MedicalRecord(id, patientId, medicalRecordNumber, startedAt,
            lastClinicalActivityAt, createdAt, updatedAt, odontogram);
    }

    public void AssignMedicalRecordNumber(long number)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Medical record number must be positive.");
        }

        if (MedicalRecordNumber.HasValue)
        {
            throw new InvalidOperationException("Medical record number has already been assigned.");
        }

        MedicalRecordNumber = number;
    }

    public void RecordClinicalActivity(DateTimeOffset occurredAt, DateTimeOffset? updatedAt = null)
    {
        var activity = occurredAt.ToUniversalTime();
        if (LastClinicalActivityAt is null || activity > LastClinicalActivityAt.Value)
            LastClinicalActivityAt = activity;
        UpdatedAt = (updatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
