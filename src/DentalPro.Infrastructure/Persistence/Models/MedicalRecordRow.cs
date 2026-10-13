namespace DentalPro.Infrastructure.Persistence.Models;

public sealed class MedicalRecordRow
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public long MedicalRecordNumber { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? LastClinicalActivityAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public PatientRow Patient { get; set; } = null!;
    public OdontogramRow Odontogram { get; set; } = null!;
}
