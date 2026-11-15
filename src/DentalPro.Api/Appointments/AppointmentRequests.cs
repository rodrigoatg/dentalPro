namespace DentalPro.Api.Appointments;

public sealed record CreateAppointmentRequest(
    Guid PatientId,
    Guid DentistId,
    DateTimeOffset StartDateTime,
    int? DurationMinutes = null,
    string? Notes = null);

public sealed record RescheduleAppointmentRequest(
    Guid DentistId,
    DateTimeOffset StartDateTime,
    int? DurationMinutes = null,
    string? Notes = null);
