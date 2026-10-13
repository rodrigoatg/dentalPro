namespace DentalPro.Api.Patients;

public sealed record PatientListItemResponse(
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
