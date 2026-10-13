using DentalPro.Domain.Patients;

namespace DentalPro.Application.Patients;

public sealed class CreatePatient(IPatientRepository patientRepository)
{
    public async Task<Patient> ExecuteAsync(
        string documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateOnly? dateOfBirth = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        CancellationToken cancellationToken = default)
    {
        var patient = Patient.Create(
            documentType,
            documentNumber,
            firstName,
            lastName,
            dateOfBirth,
            phone,
            email,
            address);

        await patientRepository.AddAsync(patient, cancellationToken);
        return patient;
    }
}
