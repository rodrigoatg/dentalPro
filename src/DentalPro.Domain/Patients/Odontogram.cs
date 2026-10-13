namespace DentalPro.Domain.Patients;

public sealed class Odontogram
{
    private Odontogram(Guid id, Guid medicalRecordId, DateTimeOffset createdAt)
    {
        Id = id;
        MedicalRecordId = medicalRecordId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid MedicalRecordId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static Odontogram Create(Guid medicalRecordId, DateTimeOffset createdAt)
    {
        if (medicalRecordId == Guid.Empty)
        {
            throw new ArgumentException("Medical record is required.", nameof(medicalRecordId));
        }

        return new Odontogram(Guid.NewGuid(), medicalRecordId, createdAt);
    }
}
