using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public interface IAppointmentRepository
{
    Task<IReadOnlyList<Appointment>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? dentistId,
        CancellationToken cancellationToken = default);

    Task<Appointment?> GetByIdForUpdateAsync(Guid appointmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> GetBlockingAsync(
        Guid dentistId,
        Guid? patientId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? excludingAppointmentId = null,
        CancellationToken cancellationToken = default);

    void Add(Appointment appointment);
}
