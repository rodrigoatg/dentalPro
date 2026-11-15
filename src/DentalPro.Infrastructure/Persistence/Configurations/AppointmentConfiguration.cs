using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<AppointmentRow>
{
    private const string StatusCheck =
        "\"Status\" IN ('Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'Rescheduled')";

    private const string RescheduledFromCheck =
        "\"RescheduledFromAppointmentId\" IS NULL OR " +
        "(\"RescheduledFromAppointmentId\" <> '00000000-0000-0000-0000-000000000000' " +
        "AND \"RescheduledFromAppointmentId\" <> \"Id\")";

    public void Configure(EntityTypeBuilder<AppointmentRow> builder)
    {
        builder.ToTable("Appointments", table =>
        {
            table.HasCheckConstraint(
                "CK_Appointments_EndAfterStart",
                "\"StartDateTime\" < \"EndDateTime\"");
            table.HasCheckConstraint("CK_Appointments_DurationMinutes_Positive", "\"DurationMinutes\" > 0");
            table.HasCheckConstraint("CK_Appointments_DurationMatchesInterval",
                "\"EndDateTime\" = \"StartDateTime\" + make_interval(mins => \"DurationMinutes\")");
            table.HasCheckConstraint("CK_Appointments_Status", StatusCheck);
            table.HasCheckConstraint("CK_Appointments_RescheduledFrom_Valid", RescheduledFromCheck);
        });

        builder.HasKey(appointment => appointment.Id).HasName("PK_Appointments");
        builder.Property(appointment => appointment.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(appointment => appointment.PatientId).HasColumnType("uuid").IsRequired();
        builder.Property(appointment => appointment.DentistId).HasColumnType("uuid").IsRequired();
        builder.Property(appointment => appointment.StartDateTime).HasColumnType("timestamptz").IsRequired();
        builder.Property(appointment => appointment.EndDateTime).HasColumnType("timestamptz").IsRequired();
        builder.Property(appointment => appointment.DurationMinutes).HasColumnType("integer").IsRequired();
        builder.Property(appointment => appointment.Status)
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();
        builder.Property(appointment => appointment.Notes).HasColumnType("text");
        builder.Property(appointment => appointment.RescheduledFromAppointmentId).HasColumnType("uuid");
        builder.Property(appointment => appointment.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(appointment => appointment.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne<PatientRow>()
            .WithMany()
            .HasForeignKey(appointment => appointment.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Appointments_Patients_PatientId");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(appointment => appointment.DentistId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Appointments_AspNetUsers_DentistId");

        builder.HasOne<AppointmentRow>()
            .WithMany()
            .HasForeignKey(appointment => appointment.RescheduledFromAppointmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Appointments_Appointments_RescheduledFromAppointmentId");

        builder.HasIndex(appointment => appointment.RescheduledFromAppointmentId)
            .IsUnique()
            .HasFilter("\"RescheduledFromAppointmentId\" IS NOT NULL")
            .HasDatabaseName("UX_Appointments_RescheduledFromAppointmentId");

        builder.HasIndex(appointment => new { appointment.DentistId, appointment.StartDateTime })
            .HasDatabaseName("IX_Appointments_DentistId_StartDateTime");

        builder.HasIndex(appointment => new { appointment.PatientId, appointment.StartDateTime })
            .HasDatabaseName("IX_Appointments_PatientId_StartDateTime");
    }
}
