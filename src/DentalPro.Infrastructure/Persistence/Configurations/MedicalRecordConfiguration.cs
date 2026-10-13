using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecordRow>
{
    public void Configure(EntityTypeBuilder<MedicalRecordRow> builder)
    {
        builder.ToTable("MedicalRecords", table =>
            table.HasCheckConstraint("CK_MedicalRecords_MedicalRecordNumber_Positive", "\"MedicalRecordNumber\" > 0"));
        builder.HasKey(record => record.Id).HasName("PK_MedicalRecords");
        builder.Property(record => record.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(record => record.PatientId).HasColumnType("uuid").IsRequired();
        builder.Property(record => record.MedicalRecordNumber).HasColumnType("bigint").IsRequired();
        builder.Property(record => record.StartedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(record => record.LastClinicalActivityAt).HasColumnType("timestamptz");
        builder.Property(record => record.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(record => record.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(record => record.PatientId)
            .IsUnique()
            .HasDatabaseName("UX_MedicalRecords_PatientId");
        builder.HasIndex(record => record.MedicalRecordNumber)
            .IsUnique()
            .HasDatabaseName("UX_MedicalRecords_MedicalRecordNumber");

        builder.HasOne(record => record.Odontogram)
            .WithOne(odontogram => odontogram.MedicalRecord)
            .HasForeignKey<OdontogramRow>(odontogram => odontogram.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Odontograms_MedicalRecords_MedicalRecordId");
    }
}
