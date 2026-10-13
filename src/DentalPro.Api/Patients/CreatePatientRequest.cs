namespace DentalPro.Api.Patients;

public sealed record CreatePatientRequest(
    string DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Phone,
    string? Email,
    string? Address);
