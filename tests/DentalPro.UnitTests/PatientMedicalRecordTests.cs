using DentalPro.Domain.Patients;

namespace DentalPro.UnitTests;

public sealed class PatientMedicalRecordTests
{
    [Fact]
    public void Create_CreatesPatientMedicalRecordAndEmptyOdontogramTogether()
    {
        var patient = Patient.Create("DNI", "12345678", "Ada", "Lovelace");
        var record = patient.MedicalRecord;
        var odontogram = record.Odontogram;

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(patient.Id, record.PatientId);
        Assert.NotEqual(Guid.Empty, odontogram.Id);
        Assert.Equal(record.Id, odontogram.MedicalRecordId);
        Assert.Null(record.MedicalRecordNumber);
        Assert.Null(record.LastClinicalActivityAt);
        Assert.Equal(record.StartedAt, record.CreatedAt);
        Assert.Equal(record.CreatedAt, record.UpdatedAt);
        Assert.Equal(record.CreatedAt, odontogram.CreatedAt);
        Assert.Equal(odontogram.CreatedAt, odontogram.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, record.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, odontogram.CreatedAt.Offset);
    }

    [Fact]
    public void AssignMedicalRecordNumber_AssignsPositiveNumberOnce()
    {
        var record = Patient.Create("DNI", "12345678", "Ada", "Lovelace").MedicalRecord;

        record.AssignMedicalRecordNumber(123);

        Assert.Equal(123, record.MedicalRecordNumber);
        Assert.Throws<InvalidOperationException>(() => record.AssignMedicalRecordNumber(124));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AssignMedicalRecordNumber_RejectsNonPositiveNumber(long number)
    {
        var record = Patient.Create("DNI", "12345678", "Ada", "Lovelace").MedicalRecord;

        Assert.Throws<ArgumentOutOfRangeException>(() => record.AssignMedicalRecordNumber(number));
        Assert.Null(record.MedicalRecordNumber);
    }
}
