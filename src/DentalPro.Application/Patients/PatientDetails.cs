namespace DentalPro.Application.Patients;

public sealed record PatientDetails(
    Guid Id, string DocumentType, string DocumentNumber, string FirstName, string LastName,
    DateOnly? DateOfBirth, string? Phone, string? Email, string? Address, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long MedicalRecordNumber);

public sealed record UpdatePatientData(
    string DocumentType, string DocumentNumber, string FirstName, string LastName,
    DateOnly? DateOfBirth, string? Phone, string? Email, string? Address);
