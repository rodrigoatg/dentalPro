using DentalPro.Domain.Patients;

namespace DentalPro.Application.Patients;

public sealed class UpdatePatient(IPatientRepository repository)
{
    public async Task ExecuteAsync(Guid id, UpdatePatientData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        Patient.ValidatePersonalInformation(data.DocumentType, data.DocumentNumber, data.FirstName,
            data.LastName, data.DateOfBirth);
        await repository.UpdateAsync(id, data with
        {
            DocumentType = data.DocumentType.Trim(), DocumentNumber = data.DocumentNumber.Trim(),
            FirstName = data.FirstName.Trim(), LastName = data.LastName.Trim(),
            Phone = Normalize(data.Phone), Email = Normalize(data.Email), Address = Normalize(data.Address)
        }, cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
