using DentalPro.Domain.Patients;

namespace DentalPro.Application.Patients;

public interface IPatientRepository
{
    Task AddAsync(Patient patient, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientListItem>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    Task<PatientDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdatePatientData data, CancellationToken cancellationToken = default);
}
