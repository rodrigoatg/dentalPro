namespace DentalPro.Api.Appointments;

public sealed record ClinicScheduleResponse(
    Guid Id,
    string TimeZoneId,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes,
    IReadOnlyList<WeeklyOpeningIntervalResponse> WeeklyIntervals,
    IReadOnlyList<ClinicDateExceptionResponse> DateExceptions);

public sealed record WeeklyOpeningIntervalResponse(string DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed record ClinicDateExceptionResponse(
    DateOnly Date,
    IReadOnlyList<ClinicOpeningIntervalResponse> OpeningIntervals);

public sealed record ClinicOpeningIntervalResponse(TimeOnly Start, TimeOnly End);

public sealed record ClinicScheduleSettingsResponse(
    string TimeZoneId,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes);
