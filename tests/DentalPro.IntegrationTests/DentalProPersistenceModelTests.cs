using DentalPro.Infrastructure.Persistence;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DentalPro.IntegrationTests;

public sealed class DentalProPersistenceModelTests
{
    private static DentalProDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DentalProDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dentalpro_dev")
            .Options;

        return new DentalProDbContext(options);
    }

    [Fact]
    public void Model_MapsThreeTablesAndUniqueIndexes()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.Equal("Patients", model.FindEntityType(typeof(PatientRow))!.GetTableName());
        Assert.Equal("MedicalRecords", model.FindEntityType(typeof(MedicalRecordRow))!.GetTableName());
        Assert.Equal("Odontograms", model.FindEntityType(typeof(OdontogramRow))!.GetTableName());

        AssertUniqueIndex<PatientRow>(model, nameof(PatientRow.DocumentType), nameof(PatientRow.DocumentNumber));
        AssertUniqueIndex<MedicalRecordRow>(model, nameof(MedicalRecordRow.PatientId));
        AssertUniqueIndex<MedicalRecordRow>(model, nameof(MedicalRecordRow.MedicalRecordNumber));
        AssertUniqueIndex<OdontogramRow>(model, nameof(OdontogramRow.MedicalRecordId));
    }

    [Fact]
    public void Model_UsesRequiredRestrictedRelationshipsAndExpectedPostgresTypes()
    {
        using var context = CreateContext();
        var model = context.Model;
        var recordType = model.FindEntityType(typeof(MedicalRecordRow))!;
        var odontogramType = model.FindEntityType(typeof(OdontogramRow))!;

        var patientForeignKey = recordType.GetForeignKeys().Single();
        var odontogramForeignKey = odontogramType.GetForeignKeys().Single();

        Assert.True(patientForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, patientForeignKey.DeleteBehavior);
        Assert.True(odontogramForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, odontogramForeignKey.DeleteBehavior);
        Assert.Equal("bigint", recordType.FindProperty(nameof(MedicalRecordRow.MedicalRecordNumber))!.GetColumnType());
        Assert.Equal("timestamptz", recordType.FindProperty(nameof(MedicalRecordRow.StartedAt))!.GetColumnType());
        Assert.True(recordType.FindProperty(nameof(MedicalRecordRow.LastClinicalActivityAt))!.IsNullable);
    }

    private static void AssertUniqueIndex<TEntity>(IModel model, params string[] propertyNames)
    {
        var entityType = model.FindEntityType(typeof(TEntity))!;
        var index = entityType.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.True(index.IsUnique);
    }
}
