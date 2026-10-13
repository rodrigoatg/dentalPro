using DentalPro.Domain.Patients;

namespace DentalPro.Application.Patients;

public interface IPatientRepository
{
    Task AddAsync(Patient patient, CancellationToken cancellationToken = default);
}
