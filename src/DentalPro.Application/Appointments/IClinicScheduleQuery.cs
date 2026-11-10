namespace DentalPro.Application.Appointments;

/// <summary>Returns clinic opening intervals resolved to UTC instants.</summary>
public interface IClinicScheduleQuery
{
    Task<IReadOnlyList<AppointmentInterval>> GetOpenIntervalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
