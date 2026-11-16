namespace DentalPro.Application.Patients;

public sealed class GetPatient(IPatientRepository repository)
{
    public async Task<PatientDetails> ExecuteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw new PatientNotFoundException();
}
