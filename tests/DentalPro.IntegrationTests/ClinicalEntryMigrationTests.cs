using DentalPro.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DentalPro.IntegrationTests;

public sealed class ClinicalEntryMigrationTests
{
    [Fact]
    public void Up_creates_only_new_clinical_tables_and_installs_immutability_guards()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new InspectableMigration().ApplyUp(builder);

        var createdTables = builder.Operations.OfType<CreateTableOperation>()
            .Select(operation => operation.Name).ToHashSet(StringComparer.Ordinal);
        Assert.True(new[]
        {
            "ClinicalEntries", "Treatments", "ClinicalEntryVersions", "ClinicalEntryTreatments",
            "TreatmentTeeth", "ClinicalEntryVersionTreatments"
        }.ToHashSet(StringComparer.Ordinal).SetEquals(createdTables));
        var foreignKeys = builder.Operations.OfType<CreateTableOperation>()
            .SelectMany(operation => operation.ForeignKeys).ToArray();
        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete));
        Assert.Contains(foreignKeys, foreignKey => foreignKey.PrincipalTable == "MedicalRecords");
        Assert.Contains(foreignKeys, foreignKey => foreignKey.PrincipalTable == "Appointments");
        Assert.Contains(foreignKeys, foreignKey => foreignKey.PrincipalTable == "AspNetUsers");

        var sql = string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        Assert.Contains("TR_ClinicalEntryVersions_Immutable", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ClinicalEntryVersionTreatments_Immutable", sql, StringComparison.Ordinal);
        Assert.Contains("BEFORE UPDATE OR DELETE", sql, StringComparison.Ordinal);
        Assert.Contains("BEFORE INSERT OR UPDATE OR DELETE ON \"ClinicalEntryVersionTreatments\"", sql, StringComparison.Ordinal);
        Assert.Contains("version_transaction_id = pg_current_xact_id()::text::bigint", sql, StringComparison.Ordinal);
        Assert.Contains("ERRCODE = '55000'", sql, StringComparison.Ordinal);

        var toothTable = builder.Operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "TreatmentTeeth");
        Assert.Contains(toothTable.CheckConstraints, constraint =>
            constraint.Name == "CK_TreatmentTeeth_CompletedAt_Consistency");
        Assert.Equal(new[] { "TreatmentId", "ToothNumber" }, toothTable.PrimaryKey!.Columns);

        var versionTable = builder.Operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "ClinicalEntryVersions");
        Assert.Contains(versionTable.Columns, column => column.Name == "CreatedTransactionId" &&
            column.DefaultValueSql == "pg_current_xact_id()::text::bigint");
    }

    [Fact]
    public void Down_drops_only_tables_created_by_the_clinical_migration()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new InspectableMigration().ApplyDown(builder);

        var droppedTables = builder.Operations.OfType<DropTableOperation>()
            .Select(operation => operation.Name).ToHashSet(StringComparer.Ordinal);
        Assert.True(new[]
        {
            "ClinicalEntryTreatments", "ClinicalEntryVersionTreatments", "TreatmentTeeth",
            "ClinicalEntryVersions", "Treatments", "ClinicalEntries"
        }.ToHashSet(StringComparer.Ordinal).SetEquals(droppedTables));
        Assert.DoesNotContain(droppedTables, name => name is "MedicalRecords" or "Odontograms" or "Patients" or "Appointments");
        Assert.Contains(builder.Operations.OfType<SqlOperation>(), operation =>
            operation.Sql.Contains("DROP FUNCTION IF EXISTS \"PreventClinicalEntryVersionMutation\"", StringComparison.Ordinal));
    }

    private sealed class InspectableMigration : AddClinicalRecordsAndTreatments
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
        public void ApplyDown(MigrationBuilder builder) => Down(builder);
    }
}
