using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public interface IClinicScheduleRepository
{
    Task<ClinicSchedule?> GetForUpdateAsync(CancellationToken cancellationToken = default);
    Task<ClinicSchedule?> GetAsync(CancellationToken cancellationToken = default);
    void Add(ClinicSchedule schedule);
    Task UpdateAsync(ClinicSchedule schedule, CancellationToken cancellationToken = default);
}
