namespace DentalPro.Application.Patients;

public sealed class ListPatients(IPatientRepository patientRepository)
{
    public Task<IReadOnlyList<PatientListItem>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        patientRepository.ListAsync(cancellationToken);
}
