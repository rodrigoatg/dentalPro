namespace DentalPro.Domain.Patients;

public sealed class MedicalRecord
{
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
}
