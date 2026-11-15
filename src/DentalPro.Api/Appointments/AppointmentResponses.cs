namespace DentalPro.Api.Appointments;

public sealed record AppointmentResponse(
    Guid AppointmentId,
    Guid PatientId,
    string PatientFirstName,
    string PatientLastName,
    Guid DentistId,
    string DentistFirstName,
    string DentistLastName,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    int DurationMinutes,
    string Status,
    string? Notes,
    Guid? RescheduledFromAppointmentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AppointmentDentistOptionResponse(Guid Id, string FirstName, string LastName);

public sealed record AppointmentAvailabilityResponse(
    IReadOnlyList<AppointmentIntervalResponse> AvailableIntervals,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes);

public sealed record AppointmentIntervalResponse(DateTimeOffset Start, DateTimeOffset End);
