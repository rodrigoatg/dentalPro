namespace DentalPro.Domain.Patients;

public sealed class Patient
{
    private Patient(
        Guid id,
        string documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateOnly? dateOfBirth,
        string? phone,
        string? email,
        string? address,
        DateTimeOffset createdAt)
    {
        Id = id;
        DocumentType = documentType;
        DocumentNumber = documentNumber;
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        Phone = phone;
        Email = email;
        Address = address;
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string DocumentType { get; private set; }

    public string DocumentNumber { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Patient Create(
        string documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateOnly? dateOfBirth = null,
        string? phone = null,
        string? email = null,
        string? address = null)
    {
        EnsureRequiredName(firstName, nameof(firstName));
        EnsureRequiredName(lastName, nameof(lastName));

        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
        }

        var now = DateTimeOffset.UtcNow;

        return new Patient(
            Guid.NewGuid(),
            documentType,
            documentNumber,
            firstName,
            lastName,
            dateOfBirth,
            phone,
            email,
            address,
            now);
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void EnsureRequiredName(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", parameterName);
        }
    }
}
