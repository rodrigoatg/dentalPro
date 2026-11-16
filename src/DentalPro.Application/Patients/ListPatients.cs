namespace DentalPro.Application.Patients;

public sealed class ListPatients(IPatientRepository patientRepository)
{
    public Task<IReadOnlyList<PatientListItem>> ExecuteAsync(
        string? search = null, CancellationToken cancellationToken = default) =>
        patientRepository.SearchAsync(search, cancellationToken);
}
