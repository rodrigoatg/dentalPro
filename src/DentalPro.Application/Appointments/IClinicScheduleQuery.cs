namespace DentalPro.Application.Appointments;

/// <summary>Returns clinic opening intervals resolved to UTC instants.</summary>
public interface IClinicScheduleQuery
{
    Task<ClinicScheduleAvailabilitySnapshot> GetAvailabilitySnapshotAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<ClinicScheduleSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentInterval>> GetOpenIntervalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

public sealed record ClinicScheduleAvailabilitySnapshot(
    ClinicScheduleSettings Settings,
    IReadOnlyList<AppointmentInterval> OpenIntervals);

public sealed record ClinicScheduleSettings(
    string TimeZoneId,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes);

public static class ClinicScheduleTime
{
    public static DateTimeOffset ResolveLocalStart(DateTimeOffset suppliedLocalTime, ClinicScheduleSettings settings)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ClinicScheduleConfigurationException($"Configured time zone '{settings.TimeZoneId}' is not available.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ClinicScheduleConfigurationException($"Configured time zone '{settings.TimeZoneId}' is invalid.", exception);
        }

        var local = DateTime.SpecifyKind(suppliedLocalTime.DateTime, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw new AppointmentLocalTimeInvalidException(local);
        if (zone.IsAmbiguousTime(local)) throw new AppointmentLocalTimeAmbiguousException(local);
        var expectedOffset = zone.GetUtcOffset(local);
        if (suppliedLocalTime.Offset != expectedOffset)
            throw new AppointmentLocalTimeOffsetMismatchException(local, expectedOffset);
        return new DateTimeOffset(local, expectedOffset).ToUniversalTime();
    }

    public static DateTimeOffset CalculateEnd(DateTimeOffset startUtc, int durationMinutes)
    {
        if (durationMinutes <= 0) throw new AppointmentDurationInvalidException();
        try { return startUtc.AddMinutes(durationMinutes); }
        catch (ArgumentOutOfRangeException) { throw new AppointmentDurationInvalidException(); }
    }
}
