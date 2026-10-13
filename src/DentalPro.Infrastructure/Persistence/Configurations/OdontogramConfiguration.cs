using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class OdontogramConfiguration : IEntityTypeConfiguration<OdontogramRow>
{
    public void Configure(EntityTypeBuilder<OdontogramRow> builder)
    {
        builder.ToTable("Odontograms");
        builder.HasKey(odontogram => odontogram.Id).HasName("PK_Odontograms");
        builder.Property(odontogram => odontogram.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(odontogram => odontogram.MedicalRecordId).HasColumnType("uuid").IsRequired();
        builder.Property(odontogram => odontogram.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(odontogram => odontogram.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(odontogram => odontogram.MedicalRecordId)
            .IsUnique()
            .HasDatabaseName("UX_Odontograms_MedicalRecordId");
    }
}
