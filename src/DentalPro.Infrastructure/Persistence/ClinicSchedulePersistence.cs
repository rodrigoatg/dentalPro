using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DentalPro.Infrastructure.Persistence;

public sealed class ClinicSchedulePersistence(DentalProDbContext dbContext)
    : IClinicScheduleRepository, IClinicScheduleQuery
{
    private const short SingletonKey = 1;
    private ClinicScheduleRow? _lockedRow;
    private ClinicSchedule? _lockedAggregate;
    private IDbContextTransaction? _lockedTransaction;

    public async Task<ClinicSchedule?> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(false, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task<ClinicSchedule?> GetForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var transaction = dbContext.Database.CurrentTransaction;
        if (transaction is null)
            throw new InvalidOperationException("Clinic schedule updates require the shared appointment unit-of-work transaction.");

        if (_lockedRow is not null && dbContext.Entry(_lockedRow).State != EntityState.Detached
            && ReferenceEquals(_lockedTransaction, transaction))
            return _lockedAggregate;

        DetachTrackedScheduleGraph();

        var locked = await dbContext.ClinicSchedules
            .FromSqlInterpolated($"SELECT * FROM \"ClinicSchedules\" WHERE \"SingletonKey\" = {SingletonKey} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (locked is null) return null;

        var row = await dbContext.ClinicSchedules
            .Include(x => x.WeeklyIntervals)
            .Include(x => x.DateExceptions).ThenInclude(x => x.Intervals)
            .SingleAsync(x => x.Id == locked.Id, cancellationToken);
        _lockedRow = row;
        _lockedAggregate = ToDomain(row);
        _lockedTransaction = transaction;
        return _lockedAggregate;
    }

    public void Add(ClinicSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (dbContext.ClinicSchedules.Local.Any())
            throw new InvalidOperationException("A clinic schedule row is already tracked in this scope.");
        var row = ToRow(schedule);
        dbContext.ClinicSchedules.Add(row);
        _lockedRow = row;
        _lockedAggregate = schedule;
        _lockedTransaction = dbContext.Database.CurrentTransaction;
    }

    public Task UpdateAsync(ClinicSchedule schedule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        cancellationToken.ThrowIfCancellationRequested();
        var currentTransaction = dbContext.Database.CurrentTransaction;
        if (currentTransaction is null || !ReferenceEquals(_lockedTransaction, currentTransaction)
            || _lockedRow is null || _lockedAggregate is null || _lockedRow.Id != schedule.Id
            || dbContext.Entry(_lockedRow).State == EntityState.Detached)
            throw new InvalidOperationException("The clinic schedule must be loaded for update under its active lock transaction.");

        // Domain updates are immutable snapshots; keep the locked root row and replace only its child sets.
        _lockedRow.TimeZoneId = schedule.TimeZoneId;
        _lockedRow.DefaultAppointmentDurationMinutes = schedule.DefaultDurationMinutes;
        _lockedRow.StartIncrementMinutes = schedule.StartIncrementMinutes;
        dbContext.ClinicWeeklyIntervals.RemoveRange(_lockedRow.WeeklyIntervals);
        dbContext.ClinicScheduleExceptions.RemoveRange(_lockedRow.DateExceptions);
        _lockedRow.WeeklyIntervals = schedule.WeeklyIntervals.Select(item => new ClinicWeeklyIntervalRow
        {
            Id = Guid.NewGuid(), ClinicScheduleId = schedule.Id, DayOfWeek = (short)item.DayOfWeek,
            Start = item.Start, End = item.End
        }).ToList();
        _lockedRow.DateExceptions = schedule.DateExceptions.Select(item => new ClinicScheduleDateExceptionRow
        {
            Id = Guid.NewGuid(), ClinicScheduleId = schedule.Id, Date = item.Date,
            Intervals = item.Intervals.Select(interval => new ClinicScheduleExceptionIntervalRow
            {
                Id = Guid.NewGuid(), Start = interval.Start, End = interval.End
            }).ToList()
        }).ToList();
        _lockedAggregate = schedule;
        return Task.CompletedTask;
    }

    public async Task<ClinicScheduleSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(dbContext.Database.CurrentTransaction is not null, cancellationToken)
            ?? throw new ClinicScheduleNotConfiguredException();
        return new ClinicScheduleSettings(row.TimeZoneId, row.DefaultAppointmentDurationMinutes, row.StartIncrementMinutes);
    }

    public async Task<IReadOnlyList<AppointmentInterval>> GetOpenIntervalsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(dbContext.Database.CurrentTransaction is not null, cancellationToken)
            ?? throw new ClinicScheduleNotConfiguredException();
        return ResolveOpenIntervals(row, from, to);
    }

    public async Task<ClinicScheduleAvailabilitySnapshot> GetAvailabilitySnapshotAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(dbContext.Database.CurrentTransaction is not null, cancellationToken)
            ?? throw new ClinicScheduleNotConfiguredException();
        var settings = new ClinicScheduleSettings(
            row.TimeZoneId, row.DefaultAppointmentDurationMinutes, row.StartIncrementMinutes);
        return new ClinicScheduleAvailabilitySnapshot(settings, ResolveOpenIntervals(row, from, to));
    }

    private static IReadOnlyList<AppointmentInterval> ResolveOpenIntervals(
        ClinicScheduleRow row, DateTimeOffset from, DateTimeOffset to)
    {
        if (from >= to) return [];
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ClinicScheduleConfigurationException("The stored clinic time zone is invalid or unavailable.", exception);
        }

        var localFrom = TimeZoneInfo.ConvertTime(from.ToUniversalTime(), zone).Date;
        var localTo = TimeZoneInfo.ConvertTime(to.ToUniversalTime(), zone).Date;
        var weekly = row.WeeklyIntervals.GroupBy(x => (DayOfWeek)x.DayOfWeek)
            .ToDictionary(x => x.Key, x => x.OrderBy(item => item.Start).ToArray());
        var exceptions = row.DateExceptions.ToDictionary(x => x.Date);
        var resolved = new List<AppointmentInterval>();

        for (var day = DateOnly.FromDateTime(localFrom); day <= DateOnly.FromDateTime(localTo); day = day.AddDays(1))
        {
            IEnumerable<(TimeOnly Start, TimeOnly End)> intervals;
            if (exceptions.TryGetValue(day, out var special))
                intervals = special.Intervals.Select(x => (x.Start, x.End));
            else if (weekly.TryGetValue(day.DayOfWeek, out var usual))
                intervals = usual.Select(x => (x.Start, x.End));
            else
                continue;

            foreach (var (start, end) in intervals)
            {
                var localStart = DateTime.SpecifyKind(day.ToDateTime(start), DateTimeKind.Unspecified);
                var localEnd = DateTime.SpecifyKind(day.ToDateTime(end), DateTimeKind.Unspecified);
                var utcStart = ResolveBoundary(localStart, zone);
                var utcEnd = ResolveBoundary(localEnd, zone);
                var clippedStart = utcStart < from.ToUniversalTime() ? from.ToUniversalTime() : utcStart;
                var clippedEnd = utcEnd > to.ToUniversalTime() ? to.ToUniversalTime() : utcEnd;
                if (clippedStart < clippedEnd) resolved.Add(new AppointmentInterval(clippedStart, clippedEnd));
            }
        }

        var ordered = resolved.OrderBy(x => x.Start).ToArray();
        for (var index = 1; index < ordered.Length; index++)
            if (ordered[index].Start < ordered[index - 1].End)
                throw new ClinicScheduleConfigurationException("Resolved clinic openings overlap in UTC.");
        return ordered;
    }

    private void DetachTrackedScheduleGraph()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<ClinicScheduleExceptionIntervalRow>().ToArray())
            entry.State = EntityState.Detached;
        foreach (var entry in dbContext.ChangeTracker.Entries<ClinicScheduleDateExceptionRow>().ToArray())
            entry.State = EntityState.Detached;
        foreach (var entry in dbContext.ChangeTracker.Entries<ClinicWeeklyIntervalRow>().ToArray())
            entry.State = EntityState.Detached;
        foreach (var entry in dbContext.ChangeTracker.Entries<ClinicScheduleRow>().ToArray())
            entry.State = EntityState.Detached;
        _lockedRow = null;
        _lockedAggregate = null;
        _lockedTransaction = null;
    }

    private async Task<ClinicScheduleRow?> LoadRowAsync(bool sharedLock, CancellationToken cancellationToken)
    {
        if (sharedLock)
        {
            var tx = dbContext.Database.CurrentTransaction
                ?? throw new InvalidOperationException("The clinic schedule lock requires an active unit-of-work transaction.");
            _ = tx;
            var locked = await dbContext.ClinicSchedules
                .FromSqlInterpolated($"SELECT * FROM \"ClinicSchedules\" WHERE \"SingletonKey\" = {SingletonKey} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (locked is null) return null;
            return await dbContext.ClinicSchedules
                .Include(x => x.WeeklyIntervals)
                .Include(x => x.DateExceptions).ThenInclude(x => x.Intervals)
                .AsNoTracking().SingleAsync(x => x.Id == locked.Id, cancellationToken);
        }

        return await dbContext.ClinicSchedules.AsNoTracking()
            .Include(x => x.WeeklyIntervals)
            .Include(x => x.DateExceptions).ThenInclude(x => x.Intervals)
            .SingleOrDefaultAsync(x => x.SingletonKey == SingletonKey, cancellationToken);
    }

    private static DateTimeOffset ResolveBoundary(DateTime local, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(local))
            throw new ClinicScheduleConfigurationException($"Stored opening boundary '{local:O}' falls in a nonexistent local time.");
        if (zone.IsAmbiguousTime(local))
            throw new ClinicScheduleConfigurationException($"Stored opening boundary '{local:O}' is ambiguous in the clinic time zone.");
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static ClinicSchedule ToDomain(ClinicScheduleRow row) => ClinicSchedule.Rehydrate(
        row.Id, row.TimeZoneId, row.DefaultAppointmentDurationMinutes, row.StartIncrementMinutes,
        row.WeeklyIntervals.Select(x => new WeeklyOpeningInterval((DayOfWeek)x.DayOfWeek, x.Start, x.End)),
        row.DateExceptions.Select(x => new ClinicDateException(x.Date, x.Intervals.Select(i => (i.Start, i.End)))));

    private static ClinicScheduleRow ToRow(ClinicSchedule schedule) => new()
    {
        Id = schedule.Id, SingletonKey = SingletonKey, TimeZoneId = schedule.TimeZoneId,
        DefaultAppointmentDurationMinutes = schedule.DefaultDurationMinutes,
        StartIncrementMinutes = schedule.StartIncrementMinutes,
        WeeklyIntervals = schedule.WeeklyIntervals.Select(item => new ClinicWeeklyIntervalRow
        {
            Id = Guid.NewGuid(), ClinicScheduleId = schedule.Id, DayOfWeek = (short)item.DayOfWeek,
            Start = item.Start, End = item.End
        }).ToList(),
        DateExceptions = schedule.DateExceptions.Select(item => new ClinicScheduleDateExceptionRow
        {
            Id = Guid.NewGuid(), ClinicScheduleId = schedule.Id, Date = item.Date,
            Intervals = item.Intervals.Select(interval => new ClinicScheduleExceptionIntervalRow
            { Id = Guid.NewGuid(), Start = interval.Start, End = interval.End }).ToList()
        }).ToList()
    };
}
