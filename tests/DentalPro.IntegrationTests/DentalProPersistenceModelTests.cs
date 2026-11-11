using DentalPro.Domain.AccessControl;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using DentalPro.Infrastructure.Persistence.Models;
using DentalPro.Domain.Appointments;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    public void Model_MapsPatientClinicalAndAppointmentTablesAndUniqueIndexes()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.Equal("Patients", model.FindEntityType(typeof(PatientRow))!.GetTableName());
        Assert.Equal("MedicalRecords", model.FindEntityType(typeof(MedicalRecordRow))!.GetTableName());
        Assert.Equal("Odontograms", model.FindEntityType(typeof(OdontogramRow))!.GetTableName());
        Assert.Equal("Appointments", model.FindEntityType(typeof(AppointmentRow))!.GetTableName());

        AssertUniqueIndex<PatientRow>(model, nameof(PatientRow.DocumentType), nameof(PatientRow.DocumentNumber));
        AssertUniqueIndex<MedicalRecordRow>(model, nameof(MedicalRecordRow.PatientId));
        AssertUniqueIndex<MedicalRecordRow>(model, nameof(MedicalRecordRow.MedicalRecordNumber));
        AssertUniqueIndex<OdontogramRow>(model, nameof(OdontogramRow.MedicalRecordId));
        AssertUniqueIndex<AppointmentRow>(model, nameof(AppointmentRow.RescheduledFromAppointmentId));
    }

    [Fact]
    public void Model_MapsAppointmentColumnsChecksIndexesAndRestrictedForeignKeys()
    {
        using var context = CreateContext();
        var model = context.Model;
        var appointmentType = model.FindEntityType(typeof(AppointmentRow))!;

        Assert.Equal("PK_Appointments", appointmentType.FindPrimaryKey()!.GetName());
        Assert.Equal("uuid", appointmentType.FindProperty(nameof(AppointmentRow.Id))!.GetColumnType());
        Assert.Equal("uuid", appointmentType.FindProperty(nameof(AppointmentRow.PatientId))!.GetColumnType());
        Assert.Equal("uuid", appointmentType.FindProperty(nameof(AppointmentRow.DentistId))!.GetColumnType());
        Assert.Equal("timestamptz", appointmentType.FindProperty(nameof(AppointmentRow.StartDateTime))!.GetColumnType());
        Assert.Equal("timestamptz", appointmentType.FindProperty(nameof(AppointmentRow.EndDateTime))!.GetColumnType());
        Assert.Equal("timestamptz", appointmentType.FindProperty(nameof(AppointmentRow.CreatedAt))!.GetColumnType());
        Assert.Equal("timestamptz", appointmentType.FindProperty(nameof(AppointmentRow.UpdatedAt))!.GetColumnType());
        var status = appointmentType.FindProperty(nameof(AppointmentRow.Status))!;
        Assert.Equal("text", status.GetColumnType());
        Assert.Equal(typeof(string), status.GetTypeMapping().Converter!.ProviderClrType);

        var designTimeAppointmentType = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(AppointmentRow))!;
        Assert.Equal(
            new[] { "CK_Appointments_EndAfterStart", "CK_Appointments_RescheduledFrom_Valid", "CK_Appointments_Status" },
            designTimeAppointmentType.GetCheckConstraints().Select(constraint => constraint.Name!).Order().ToArray());

        AssertIndex<AppointmentRow>(model, nameof(AppointmentRow.DentistId), nameof(AppointmentRow.StartDateTime), unique: false);
        AssertIndex<AppointmentRow>(model, nameof(AppointmentRow.PatientId), nameof(AppointmentRow.StartDateTime), unique: false);
        var rescheduledIndex = appointmentType.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(AppointmentRow.RescheduledFromAppointmentId)]));
        Assert.True(rescheduledIndex.IsUnique);
        Assert.Equal("\"RescheduledFromAppointmentId\" IS NOT NULL", rescheduledIndex.GetFilter());

        AssertRelationship<PatientRow>(appointmentType, nameof(AppointmentRow.PatientId));
        AssertRelationship<ApplicationUser>(appointmentType, nameof(AppointmentRow.DentistId));
        AssertRelationship<AppointmentRow>(appointmentType, nameof(AppointmentRow.RescheduledFromAppointmentId));
    }

    [Fact]
    public void Model_MapsIdentityAndRbacTablesWithoutChangingClinicalTablesOrSequence()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.Equal("AspNetUsers", model.FindEntityType(typeof(ApplicationUser))!.GetTableName());
        Assert.Equal("AspNetRoles", model.FindEntityType(typeof(IdentityRole<Guid>))!.GetTableName());
        Assert.Equal("AspNetUserRoles", model.FindEntityType(typeof(IdentityUserRole<Guid>))!.GetTableName());
        Assert.Equal("AspNetUserClaims", model.FindEntityType(typeof(IdentityUserClaim<Guid>))!.GetTableName());
        Assert.Equal("AspNetRoleClaims", model.FindEntityType(typeof(IdentityRoleClaim<Guid>))!.GetTableName());
        Assert.Equal("AspNetUserLogins", model.FindEntityType(typeof(IdentityUserLogin<Guid>))!.GetTableName());
        Assert.Equal("AspNetUserTokens", model.FindEntityType(typeof(IdentityUserToken<Guid>))!.GetTableName());
        Assert.Equal("Permissions", model.FindEntityType(typeof(Permission))!.GetTableName());
        Assert.Equal("RolePermissions", model.FindEntityType(typeof(RolePermission))!.GetTableName());

        var assignmentType = model.FindEntityType(typeof(RolePermission))!;
        Assert.Equal(
            [nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId)],
            assignmentType.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(assignmentType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(IdentityRole<Guid>) &&
            foreignKey.Properties.Single().Name == nameof(RolePermission.RoleId) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(assignmentType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Permission) &&
            foreignKey.Properties.Single().Name == nameof(RolePermission.PermissionId) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        AssertUniqueIndex<Permission>(model, nameof(Permission.Code));

        Assert.Equal("Patients", model.FindEntityType(typeof(PatientRow))!.GetTableName());
        Assert.Equal("MedicalRecords", model.FindEntityType(typeof(MedicalRecordRow))!.GetTableName());
        Assert.Equal("Odontograms", model.FindEntityType(typeof(OdontogramRow))!.GetTableName());
        var medicalRecordSequence = Assert.Single(model.GetSequences(), sequence =>
            sequence.Name == "medical_record_number_seq" && sequence.Schema == "public");
        Assert.Equal(1, medicalRecordSequence.StartValue);
        Assert.Equal(1, medicalRecordSequence.IncrementBy);
        Assert.False(medicalRecordSequence.IsCyclic);
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

    private static void AssertIndex<TEntity>(
        IModel model,
        string propertyName1,
        string propertyName2,
        bool unique)
    {
        var entityType = model.FindEntityType(typeof(TEntity))!;
        var index = entityType.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([propertyName1, propertyName2]));
        Assert.Equal(unique, index.IsUnique);
    }

    private static void AssertRelationship<TPrincipal>(IEntityType entityType, string foreignKeyProperty) =>
        Assert.Contains(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TPrincipal)
            && foreignKey.Properties.Single().Name == foreignKeyProperty
            && foreignKey.IsRequired == (foreignKeyProperty != nameof(AppointmentRow.RescheduledFromAppointmentId))
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
}
