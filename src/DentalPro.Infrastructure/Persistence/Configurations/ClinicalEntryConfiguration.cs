using DentalPro.Domain.Patients;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class ClinicalEntryConfiguration : IEntityTypeConfiguration<ClinicalEntryRow>
{
    public void Configure(EntityTypeBuilder<ClinicalEntryRow> builder)
    {
        builder.ToTable("ClinicalEntries", table =>
            table.HasCheckConstraint("CK_ClinicalEntries_Type", "\"Type\" IN ('Consultation','Treatment','Observation','FollowUp','ExternalRecord','Other')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.MedicalRecordId).HasColumnType("uuid");
        builder.Property(x => x.DentistId).HasColumnType("uuid");
        builder.Property(x => x.AppointmentId).HasColumnType("uuid");
        builder.Property(x => x.Type).HasConversion<string>().HasColumnType("text");
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz");
        builder.Property(x => x.Diagnosis).HasColumnType("text");
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.HasOne<MedicalRecordRow>().WithMany().HasForeignKey(x => x.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntries_MedicalRecords_MedicalRecordId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DentistId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntries_AspNetUsers_DentistId");
        builder.HasOne<AppointmentRow>().WithMany().HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntries_Appointments_AppointmentId");
        builder.HasMany(x => x.Versions).WithOne(x => x.ClinicalEntry).HasForeignKey(x => x.ClinicalEntryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersions_ClinicalEntries_ClinicalEntryId");
        builder.HasMany(x => x.Treatments).WithOne(x => x.ClinicalEntry).HasForeignKey(x => x.ClinicalEntryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryTreatments_ClinicalEntries_ClinicalEntryId");
        builder.HasIndex(x => new { x.MedicalRecordId, x.OccurredAt }).HasDatabaseName("IX_ClinicalEntries_MedicalRecordId_OccurredAt");
        builder.HasIndex(x => x.DentistId).HasDatabaseName("IX_ClinicalEntries_DentistId");
        builder.HasIndex(x => x.AppointmentId).HasDatabaseName("IX_ClinicalEntries_AppointmentId");
    }
}

public sealed class ClinicalEntryVersionConfiguration : IEntityTypeConfiguration<ClinicalEntryVersionRow>
{
    public void Configure(EntityTypeBuilder<ClinicalEntryVersionRow> builder)
    {
        builder.ToTable("ClinicalEntryVersions", table =>
        {
            table.HasCheckConstraint("CK_ClinicalEntryVersions_VersionNumber_Positive", "\"VersionNumber\" > 0");
            table.HasCheckConstraint("CK_ClinicalEntryVersions_Type", "\"Type\" IN ('Consultation','Treatment','Observation','FollowUp','ExternalRecord','Other')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ClinicalEntryId).HasColumnType("uuid");
        builder.Property(x => x.VersionNumber).HasColumnType("integer");
        builder.Property(x => x.MedicalRecordId).HasColumnType("uuid");
        builder.Property(x => x.DentistId).HasColumnType("uuid");
        builder.Property(x => x.AppointmentId).HasColumnType("uuid");
        builder.Property(x => x.Type).HasConversion<string>().HasColumnType("text");
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz");
        builder.Property(x => x.Diagnosis).HasColumnType("text");
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedBy).HasColumnType("uuid");
        builder.Property(x => x.CreatedTransactionId).HasColumnType("bigint")
            .HasDefaultValueSql("pg_current_xact_id()::text::bigint");
        builder.Property(x => x.ChangeReason).HasColumnType("text");
        builder.HasOne<MedicalRecordRow>().WithMany().HasForeignKey(x => x.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersions_MedicalRecords_MedicalRecordId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DentistId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersions_AspNetUsers_DentistId");
        builder.HasOne<AppointmentRow>().WithMany().HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersions_Appointments_AppointmentId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersions_AspNetUsers_CreatedBy");
        builder.HasMany(x => x.Treatments).WithOne(x => x.ClinicalEntryVersion).HasForeignKey(x => x.ClinicalEntryVersionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersionTreatments_ClinicalEntryVersions_VersionId");
        builder.HasIndex(x => new { x.ClinicalEntryId, x.VersionNumber }).IsUnique()
            .HasDatabaseName("UX_ClinicalEntryVersions_Entry_VersionNumber");
    }
}

public sealed class ClinicalEntryTreatmentConfiguration : IEntityTypeConfiguration<ClinicalEntryTreatmentRow>
{
    public void Configure(EntityTypeBuilder<ClinicalEntryTreatmentRow> builder)
    {
        builder.ToTable("ClinicalEntryTreatments");
        builder.HasKey(x => new { x.ClinicalEntryId, x.TreatmentId });
        builder.HasOne(x => x.Treatment).WithMany(x => x.ClinicalEntries).HasForeignKey(x => x.TreatmentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryTreatments_Treatments_TreatmentId");
    }
}

public sealed class ClinicalEntryVersionTreatmentConfiguration : IEntityTypeConfiguration<ClinicalEntryVersionTreatmentRow>
{
    public void Configure(EntityTypeBuilder<ClinicalEntryVersionTreatmentRow> builder)
    {
        builder.ToTable("ClinicalEntryVersionTreatments");
        builder.HasKey(x => new { x.ClinicalEntryVersionId, x.TreatmentId });
        builder.HasOne(x => x.Treatment).WithMany().HasForeignKey(x => x.TreatmentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClinicalEntryVersionTreatments_Treatments_TreatmentId");
    }
}
