using DentalPro.Domain.Treatments;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class TreatmentConfiguration : IEntityTypeConfiguration<TreatmentRow>
{
    public void Configure(EntityTypeBuilder<TreatmentRow> builder)
    {
        builder.ToTable("Treatments", table =>
        {
            table.HasCheckConstraint("CK_Treatments_Type", "\"Type\" IN ('Restoration','Extraction','RootCanal','Cleaning','Crown','Other')");
            table.HasCheckConstraint("CK_Treatments_Priority", "\"Priority\" IN ('Low','Medium','High','Urgent')");
            table.HasCheckConstraint("CK_Treatments_Status", "\"Status\" IN ('Planned','InProgress','PartiallyCompleted','Completed','Cancelled')");
            table.HasCheckConstraint("CK_Treatments_Cost_NonNegative", "\"Cost\" IS NULL OR \"Cost\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.MedicalRecordId).HasColumnType("uuid");
        builder.Property(x => x.DentistId).HasColumnType("uuid");
        builder.Property(x => x.Type).HasConversion<string>().HasColumnType("text");
        builder.Property(x => x.OccurredAt).HasColumnType("timestamptz");
        builder.Property(x => x.RecordedAt).HasColumnType("timestamptz");
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.Priority).HasConversion<string>().HasColumnType("text");
        builder.Property(x => x.Status).HasConversion<string>().HasColumnType("text");
        builder.Property(x => x.Cost).HasColumnType("numeric(12,2)");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.FinalizedAt).HasColumnType("timestamptz");
        builder.HasOne<MedicalRecordRow>().WithMany().HasForeignKey(x => x.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Treatments_MedicalRecords_MedicalRecordId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DentistId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Treatments_AspNetUsers_DentistId");
        builder.HasMany(x => x.Teeth).WithOne(x => x.Treatment).HasForeignKey(x => x.TreatmentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TreatmentTeeth_Treatments_TreatmentId");
        builder.HasIndex(x => new { x.MedicalRecordId, x.OccurredAt }).HasDatabaseName("IX_Treatments_MedicalRecordId_OccurredAt");
        builder.HasIndex(x => x.DentistId).HasDatabaseName("IX_Treatments_DentistId");
    }
}

public sealed class TreatmentToothConfiguration : IEntityTypeConfiguration<TreatmentToothRow>
{
    public void Configure(EntityTypeBuilder<TreatmentToothRow> builder)
    {
        builder.ToTable("TreatmentTeeth", table =>
        {
            table.HasCheckConstraint("CK_TreatmentTeeth_FdiPermanent", "\"ToothNumber\" / 10 BETWEEN 1 AND 4 AND \"ToothNumber\" % 10 BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_TreatmentTeeth_Status", "\"Status\" IN ('Planned','InProgress','Completed','Cancelled')");
            table.HasCheckConstraint("CK_TreatmentTeeth_CompletedAt_Consistency",
                "(\"Status\" = 'Completed') = (\"CompletedAt\" IS NOT NULL)");
        });
        builder.HasKey(x => new { x.TreatmentId, x.ToothNumber });
        builder.Property(x => x.TreatmentId).HasColumnType("uuid");
        builder.Property(x => x.ToothNumber).HasColumnType("integer");
        builder.Property(x => x.Status).HasConversion<string>().HasColumnType("text");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamptz");
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.Property(x => x.ResultingToothState).HasColumnType("text");
    }
}
