namespace DentalPro.Application.Patients;

public sealed class CountPatients(IPatientRepository repository)
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        repository.CountAsync(cancellationToken);
}
