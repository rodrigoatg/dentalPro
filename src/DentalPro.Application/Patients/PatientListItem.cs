namespace DentalPro.Application.Patients;

public sealed record PatientListItem(
    Guid Id,
    string DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    long MedicalRecordNumber);
