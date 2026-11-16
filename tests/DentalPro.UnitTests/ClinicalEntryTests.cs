using DentalPro.Domain.Patients;

namespace DentalPro.UnitTests;

public sealed class ClinicalEntryTests
{
    [Fact]
    public void Create_preserves_multiple_treatments_in_initial_version()
    {
        var medicalRecordId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        var treatmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var occurredAt = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var entry = ClinicalEntry.Create(medicalRecordId, dentistId, Guid.NewGuid(),
            ClinicalEntryType.Treatment, occurredAt, "Caries", "Restauración y control.", treatmentIds);
        var version = ClinicalEntryVersion.Initial(entry, dentistId, "Registro inicial");

        Assert.Equal(medicalRecordId, entry.MedicalRecordId);
        Assert.Equal(dentistId, entry.DentistId);
        Assert.Equal(occurredAt.ToUniversalTime(), entry.OccurredAt);
        Assert.Equal(treatmentIds, entry.TreatmentIds);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(treatmentIds, version.TreatmentIds);
        Assert.Equal("Registro inicial", version.ChangeReason);
    }

    [Fact]
    public void Create_rejects_duplicate_treatment_ids()
    {
        var treatmentId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => ClinicalEntry.Create(Guid.NewGuid(), Guid.NewGuid(), null,
            ClinicalEntryType.Consultation, DateTimeOffset.UtcNow, null, "Consulta.", [treatmentId, treatmentId]));
    }

    [Fact]
    public void Clinical_entry_versions_expose_no_mutable_properties()
    {
        Assert.All(typeof(ClinicalEntryVersion).GetProperties(), property =>
            Assert.Null(property.SetMethod));
    }
}
