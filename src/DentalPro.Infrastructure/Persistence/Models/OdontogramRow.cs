namespace DentalPro.Infrastructure.Persistence.Models;

public sealed class OdontogramRow
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public MedicalRecordRow MedicalRecord { get; set; } = null!;
}
