namespace DentalPro.Domain.Appointments;

/// <summary>Clinic-wide local opening hours and booking defaults.</summary>
public sealed class ClinicSchedule
{
    private ClinicSchedule(Guid id, string timeZoneId, int defaultDurationMinutes, int startIncrementMinutes,
        IEnumerable<WeeklyOpeningInterval> weeklyIntervals, IEnumerable<ClinicDateException> dateExceptions)
    {
        Id = id;
        TimeZoneId = timeZoneId;
        DefaultDurationMinutes = defaultDurationMinutes;
        StartIncrementMinutes = startIncrementMinutes;
        WeeklyIntervals = Array.AsReadOnly(weeklyIntervals.ToArray());
        DateExceptions = Array.AsReadOnly(dateExceptions.ToArray());
        Validate();
    }

    public Guid Id { get; }
    public string TimeZoneId { get; }
    public int DefaultDurationMinutes { get; }
    public int StartIncrementMinutes { get; }
    public IReadOnlyList<WeeklyOpeningInterval> WeeklyIntervals { get; }
    public IReadOnlyList<ClinicDateException> DateExceptions { get; }

    public static ClinicSchedule Create(string timeZoneId, int defaultDurationMinutes, int startIncrementMinutes,
        IEnumerable<WeeklyOpeningInterval>? weeklyIntervals = null,
        IEnumerable<ClinicDateException>? dateExceptions = null) =>
        new(Guid.NewGuid(), timeZoneId, defaultDurationMinutes, startIncrementMinutes,
            weeklyIntervals ?? [], dateExceptions ?? []);

    public static ClinicSchedule Rehydrate(Guid id, string timeZoneId, int defaultDurationMinutes,
        int startIncrementMinutes, IEnumerable<WeeklyOpeningInterval> weeklyIntervals,
        IEnumerable<ClinicDateException> dateExceptions) =>
        new(id, timeZoneId, defaultDurationMinutes, startIncrementMinutes, weeklyIntervals, dateExceptions);

    public ClinicSchedule Update(string timeZoneId, int defaultDurationMinutes, int startIncrementMinutes,
        IEnumerable<WeeklyOpeningInterval> weeklyIntervals, IEnumerable<ClinicDateException> dateExceptions) =>
        new(Id, timeZoneId, defaultDurationMinutes, startIncrementMinutes, weeklyIntervals, dateExceptions);

    private void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("Clinic schedule ID is required.", nameof(Id));
        if (string.IsNullOrWhiteSpace(TimeZoneId)) throw new ArgumentException("IANA time zone is required.", nameof(TimeZoneId));
        if (DefaultDurationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(DefaultDurationMinutes));
        if (StartIncrementMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(StartIncrementMinutes));
        if (WeeklyIntervals.Any(x => !Enum.IsDefined(x.DayOfWeek)))
            throw new ArgumentOutOfRangeException(nameof(WeeklyIntervals), "Weekday value is invalid.");
        if (WeeklyIntervals.GroupBy(x => x.DayOfWeek).Any(day =>
                day.Any(x => x.Start >= x.End) || HasOverlap(day.Select(x => (x.Start, x.End)))))
            throw new ArgumentException("Weekly opening intervals cannot overlap on the same day.", nameof(WeeklyIntervals));
        if (DateExceptions.Select(x => x.Date).Distinct().Count() != DateExceptions.Count)
            throw new ArgumentException("Only one schedule exception is allowed per date.", nameof(DateExceptions));
    }

    internal static void ValidateIntervals(IEnumerable<(TimeOnly Start, TimeOnly End)> intervals)
    {
        foreach (var interval in intervals)
            if (interval.Start >= interval.End)
                throw new ArgumentException("Opening interval start must be before its end.");
        if (HasOverlap(intervals)) throw new ArgumentException("Opening intervals cannot overlap.");
    }

    private static bool HasOverlap(IEnumerable<(TimeOnly Start, TimeOnly End)> intervals)
    {
        var ordered = intervals.OrderBy(x => x.Start).ToArray();
        return ordered.Zip(ordered.Skip(1), (left, right) => left.End > right.Start).Any(overlap => overlap);
    }
}

public sealed record WeeklyOpeningInterval(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

/// <summary>An exception replaces weekly hours; an empty interval set means closed.</summary>
public sealed class ClinicDateException
{
    public ClinicDateException(DateOnly date, IEnumerable<(TimeOnly Start, TimeOnly End)>? intervals = null)
    {
        Date = date;
        var items = (intervals ?? []).ToArray();
        ClinicSchedule.ValidateIntervals(items);
        Intervals = Array.AsReadOnly(items.Select(x => new DateOpeningInterval(x.Start, x.End)).ToArray());
    }

    public DateOnly Date { get; }
    public IReadOnlyList<DateOpeningInterval> Intervals { get; }
    public bool IsClosed => Intervals.Count == 0;
}

public sealed record DateOpeningInterval(TimeOnly Start, TimeOnly End);
