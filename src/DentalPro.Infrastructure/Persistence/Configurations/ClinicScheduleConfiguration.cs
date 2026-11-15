using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalPro.Infrastructure.Persistence.Configurations;

public sealed class ClinicScheduleConfiguration : IEntityTypeConfiguration<ClinicScheduleRow>
{
    public void Configure(EntityTypeBuilder<ClinicScheduleRow> builder)
    {
        builder.ToTable("ClinicSchedules", table =>
        {
            table.HasCheckConstraint("CK_ClinicSchedules_Singleton", "\"SingletonKey\" = 1");
            table.HasCheckConstraint("CK_ClinicSchedules_TimeZoneId_NotBlank", "length(btrim(\"TimeZoneId\")) > 0");
            table.HasCheckConstraint("CK_ClinicSchedules_DefaultDuration_Positive", "\"DefaultAppointmentDurationMinutes\" > 0");
            table.HasCheckConstraint("CK_ClinicSchedules_StartIncrement_Positive", "\"StartIncrementMinutes\" > 0");
        });
        builder.HasKey(x => x.Id).HasName("PK_ClinicSchedules");
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.SingletonKey).HasColumnType("smallint").IsRequired();
        builder.HasIndex(x => x.SingletonKey).IsUnique().HasDatabaseName("UX_ClinicSchedules_SingletonKey");
        builder.Property(x => x.TimeZoneId).HasColumnType("text").IsRequired();
        builder.Property(x => x.DefaultAppointmentDurationMinutes).HasColumnType("integer").IsRequired();
        builder.Property(x => x.StartIncrementMinutes).HasColumnType("integer").IsRequired();
        builder.HasMany(x => x.WeeklyIntervals).WithOne().HasForeignKey(x => x.ClinicScheduleId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ClinicWeeklyIntervals_ClinicSchedules_ClinicScheduleId");
        builder.HasMany(x => x.DateExceptions).WithOne().HasForeignKey(x => x.ClinicScheduleId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ClinicScheduleExceptions_ClinicSchedules_ClinicScheduleId");
    }
}

public sealed class ClinicWeeklyIntervalConfiguration : IEntityTypeConfiguration<ClinicWeeklyIntervalRow>
{
    public void Configure(EntityTypeBuilder<ClinicWeeklyIntervalRow> builder)
    {
        builder.ToTable("ClinicWeeklyIntervals", table =>
        {
            table.HasCheckConstraint("CK_ClinicWeeklyIntervals_DayOfWeek", "\"DayOfWeek\" BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_ClinicWeeklyIntervals_StartBeforeEnd", "\"Start\" < \"End\"");
        });
        builder.HasKey(x => x.Id).HasName("PK_ClinicWeeklyIntervals");
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ClinicScheduleId).HasColumnType("uuid");
        builder.Property(x => x.DayOfWeek).HasColumnType("smallint");
        builder.Property(x => x.Start).HasColumnType("time without time zone");
        builder.Property(x => x.End).HasColumnType("time without time zone");
        builder.HasIndex(x => new { x.ClinicScheduleId, x.DayOfWeek, x.Start })
            .HasDatabaseName("IX_ClinicWeeklyIntervals_Schedule_Day_Start");
    }
}

public sealed class ClinicScheduleDateExceptionConfiguration : IEntityTypeConfiguration<ClinicScheduleDateExceptionRow>
{
    public void Configure(EntityTypeBuilder<ClinicScheduleDateExceptionRow> builder)
    {
        builder.ToTable("ClinicScheduleExceptions");
        builder.HasKey(x => x.Id).HasName("PK_ClinicScheduleExceptions");
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ClinicScheduleId).HasColumnType("uuid");
        builder.Property(x => x.Date).HasColumnType("date");
        builder.HasIndex(x => new { x.ClinicScheduleId, x.Date }).IsUnique()
            .HasDatabaseName("UX_ClinicScheduleExceptions_Schedule_Date");
        builder.HasMany(x => x.Intervals).WithOne().HasForeignKey(x => x.ClinicScheduleDateExceptionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClinicScheduleExceptionIntervals_ClinicScheduleExceptions_ExceptionId");
    }
}

public sealed class ClinicScheduleExceptionIntervalConfiguration : IEntityTypeConfiguration<ClinicScheduleExceptionIntervalRow>
{
    public void Configure(EntityTypeBuilder<ClinicScheduleExceptionIntervalRow> builder)
    {
        builder.ToTable("ClinicScheduleExceptionIntervals", table =>
            table.HasCheckConstraint("CK_ClinicScheduleExceptionIntervals_StartBeforeEnd", "\"Start\" < \"End\""));
        builder.HasKey(x => x.Id).HasName("PK_ClinicScheduleExceptionIntervals");
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ClinicScheduleDateExceptionId).HasColumnType("uuid");
        builder.Property(x => x.Start).HasColumnType("time without time zone");
        builder.Property(x => x.End).HasColumnType("time without time zone");
        builder.HasIndex(x => new { x.ClinicScheduleDateExceptionId, x.Start })
            .HasDatabaseName("IX_ClinicScheduleExceptionIntervals_Exception_Start");
    }
}
