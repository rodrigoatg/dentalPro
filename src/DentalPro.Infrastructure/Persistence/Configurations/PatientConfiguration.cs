using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<PatientRow>
{
    public void Configure(EntityTypeBuilder<PatientRow> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(patient => patient.Id).HasName("PK_Patients");
        builder.Property(patient => patient.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(patient => patient.DocumentType).HasColumnType("text").IsRequired();
        builder.Property(patient => patient.DocumentNumber).HasColumnType("text").IsRequired();
        builder.Property(patient => patient.FirstName).HasColumnType("text").IsRequired();
        builder.Property(patient => patient.LastName).HasColumnType("text").IsRequired();
        builder.Property(patient => patient.DateOfBirth).HasColumnType("date");
        builder.Property(patient => patient.Phone).HasColumnType("text");
        builder.Property(patient => patient.Email).HasColumnType("text");
        builder.Property(patient => patient.Address).HasColumnType("text");
        builder.Property(patient => patient.IsActive).HasColumnType("boolean").IsRequired();
        builder.Property(patient => patient.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(patient => patient.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(patient => new { patient.DocumentType, patient.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("UX_Patients_DocumentType_DocumentNumber");

        builder.HasOne(patient => patient.MedicalRecord)
            .WithOne(record => record.Patient)
            .HasForeignKey<MedicalRecordRow>(record => record.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MedicalRecords_Patients_PatientId");
    }
}
