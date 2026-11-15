namespace DentalPro.Infrastructure.Persistence.Models;

public sealed class ClinicScheduleRow
{
    public Guid Id { get; set; }
    public short SingletonKey { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public int DefaultAppointmentDurationMinutes { get; set; }
    public int StartIncrementMinutes { get; set; }
    public ICollection<ClinicWeeklyIntervalRow> WeeklyIntervals { get; set; } = new List<ClinicWeeklyIntervalRow>();
    public ICollection<ClinicScheduleDateExceptionRow> DateExceptions { get; set; } = new List<ClinicScheduleDateExceptionRow>();
}

public sealed class ClinicWeeklyIntervalRow
{
    public Guid Id { get; set; }
    public Guid ClinicScheduleId { get; set; }
    public short DayOfWeek { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

public sealed class ClinicScheduleDateExceptionRow
{
    public Guid Id { get; set; }
    public Guid ClinicScheduleId { get; set; }
    public DateOnly Date { get; set; }
    public ICollection<ClinicScheduleExceptionIntervalRow> Intervals { get; set; } = new List<ClinicScheduleExceptionIntervalRow>();
}

public sealed class ClinicScheduleExceptionIntervalRow
{
    public Guid Id { get; set; }
    public Guid ClinicScheduleDateExceptionId { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}
