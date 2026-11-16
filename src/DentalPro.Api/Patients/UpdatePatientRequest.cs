namespace DentalPro.Api.Patients;

public sealed record UpdatePatientRequest(
    string? DocumentType, string? DocumentNumber, string? FirstName, string? LastName,
    DateOnly? DateOfBirth, string? Phone, string? Email, string? Address);
