namespace DentalPro.Api.Patients;

public sealed record PatientResponse(
    Guid Id,
    string DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    DateTimeOffset CreatedAt,
    long MedicalRecordNumber);
