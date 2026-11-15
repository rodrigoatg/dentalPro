namespace DentalPro.Api.Appointments;

public sealed record ConfigureClinicScheduleRequest(
    string TimeZoneId,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes,
    IReadOnlyList<WeeklyOpeningIntervalRequest> WeeklyIntervals);

public sealed record WeeklyOpeningIntervalRequest(string DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed record ClinicDateExceptionRequest(
    DateOnly Date,
    IReadOnlyList<ClinicOpeningIntervalRequest> OpeningIntervals);

public sealed record ClinicOpeningIntervalRequest(TimeOnly Start, TimeOnly End);
