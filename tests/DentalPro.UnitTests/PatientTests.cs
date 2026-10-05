using System.Reflection;
using DentalPro.Domain.Patients;

namespace DentalPro.UnitTests;

public sealed class PatientTests
{
    [Fact]
    public void Create_WithValidDetails_CreatesPatient()
    {
        var dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var patient = Patient.Create(
            "DNI",
            "12345678",
            "Ada",
            "Lovelace",
            dateOfBirth,
            "555-0100",
            "ada@example.test",
            "Example Street 1");

        Assert.NotEqual(Guid.Empty, patient.Id);
        Assert.Equal("DNI", patient.DocumentType);
        Assert.Equal("12345678", patient.DocumentNumber);
        Assert.Equal("Ada", patient.FirstName);
        Assert.Equal("Lovelace", patient.LastName);
        Assert.Equal(dateOfBirth, patient.DateOfBirth);
        Assert.Equal("555-0100", patient.Phone);
        Assert.Equal("ada@example.test", patient.Email);
        Assert.Equal("Example Street 1", patient.Address);
        Assert.Equal(patient.CreatedAt, patient.UpdatedAt);
    }

    [Fact]
    public void Create_WithoutFirstName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Patient.Create("DNI", "12345678", null!, "Lovelace"));
    }

    [Fact]
    public void Create_WithoutLastName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Patient.Create("DNI", "12345678", "Ada", null!));
    }

    [Fact]
    public void Create_WithWhitespaceOnlyFirstName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Patient.Create("DNI", "12345678", " \t", "Lovelace"));
    }

    [Fact]
    public void Create_WithWhitespaceOnlyLastName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Patient.Create("DNI", "12345678", "Ada", " \t"));
    }

    [Fact]
    public void Create_WithFutureDateOfBirth_ThrowsArgumentException()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        Assert.Throws<ArgumentException>(() => Patient.Create("DNI", "12345678", "Ada", "Lovelace", futureDate));
    }

    [Fact]
    public void Create_WithValidDateOfBirth_AcceptsDate()
    {
        var dateOfBirth = new DateOnly(2000, 1, 1);

        var patient = Patient.Create("DNI", "12345678", "Ada", "Lovelace", dateOfBirth);

        Assert.Equal(dateOfBirth, patient.DateOfBirth);
    }

    [Fact]
    public void Create_StartsActive()
    {
        var patient = Patient.Create("DNI", "12345678", "Ada", "Lovelace");

        Assert.True(patient.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var patient = Patient.Create("DNI", "12345678", "Ada", "Lovelace");

        patient.Deactivate();

        Assert.False(patient.IsActive);
    }

    [Fact]
    public void Patient_DoesNotExposePhysicalDeleteOperation()
    {
        var publicMethodNames = typeof(Patient)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name);

        Assert.DoesNotContain("Delete", publicMethodNames);
        Assert.DoesNotContain("Remove", publicMethodNames);
    }
}
