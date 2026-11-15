using System.Reflection;
using DentalPro.Application.AccessControl;
using DentalPro.Domain.AccessControl;

namespace DentalPro.UnitTests;

public sealed class PermissionTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".patients.view")]
    [InlineData("patients.view.")]
    [InlineData("patients..view")]
    [InlineData("Patients.view")]
    [InlineData("patients view")]
    [InlineData("patients_view")]
    public void Create_WithInvalidCode_ThrowsArgumentException(string code)
    {
        Assert.Throws<ArgumentException>(() => Permission.Create(code, "View patients", null, "Patients"));
    }

    [Fact]
    public void Create_WithNullCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Permission.Create(null!, "View patients", null, "Patients"));
    }

    [Fact]
    public void Create_AcceptsEveryCodeInTheBackendCatalog()
    {
        var catalog = new PermissionCatalog();

        foreach (var definition in catalog.GetAll())
        {
            var permission = Permission.Create(
                definition.Code,
                definition.Name,
                definition.Description,
                definition.Module);

            Assert.Equal(definition.Code, permission.Code);
        }
    }

    [Fact]
    public void UpdateMetadata_ChangesDisplayValues_AndPreservesIdAndCode()
    {
        var permission = Permission.Create("patients.view", "View patients", "Old description", "Patients");
        var originalId = permission.Id;
        var originalCode = permission.Code;

        permission.UpdateMetadata(" View records ", " Updated description ", " Clinical ");

        Assert.Equal(originalId, permission.Id);
        Assert.Equal(originalCode, permission.Code);
        Assert.Equal("View records", permission.Name);
        Assert.Equal("Updated description", permission.Description);
        Assert.Equal("Clinical", permission.Module);
    }

    [Fact]
    public void IdAndCode_HaveNoSetter()
    {
        Assert.Null(typeof(Permission).GetProperty(nameof(Permission.Id))!.GetSetMethod(nonPublic: true));
        Assert.Null(typeof(Permission).GetProperty(nameof(Permission.Code))!.GetSetMethod(nonPublic: true));
    }
}

public sealed class RolePermissionTests
{
    [Fact]
    public void Create_WithEmptyRoleId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RolePermission.Create(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyPermissionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RolePermission.Create(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Create_WithValidIds_CreatesAssociation()
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var association = RolePermission.Create(roleId, permissionId);

        Assert.Equal(roleId, association.RoleId);
        Assert.Equal(permissionId, association.PermissionId);
    }

    [Fact]
    public void RoleAndPermissionIds_HaveNoSetter()
    {
        Assert.Null(typeof(RolePermission).GetProperty(nameof(RolePermission.RoleId))!.GetSetMethod(nonPublic: true));
        Assert.Null(typeof(RolePermission).GetProperty(nameof(RolePermission.PermissionId))!.GetSetMethod(nonPublic: true));
    }
}

public sealed class PermissionCatalogTests
{
    private readonly PermissionCatalog _catalog = new();

    [Fact]
    public void Catalog_ContainsOnlyDocumentedImplementedPermissionCodes()
    {
        var expectedCodes = new[]
        {
            "patients.view",
            "patients.create",
            "users.view",
            "users.create",
            "users.status.manage",
            "users.roles.manage",
            "roles.view",
            "roles.permissions.manage",
            "permissions.view",
            "appointments.view", "appointments.view.all", "appointments.create", "appointments.update",
            "appointments.cancel", "appointments.confirm", "appointments.complete", "appointments.schedule.manage"
        };
        var definitions = _catalog.GetAll();

        Assert.Equal(expectedCodes.Order(), definitions.Select(definition => definition.Code).Order());
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Code).Distinct().Count());
        Assert.All(definitions, definition => PermissionCode.Create(definition.Code));
    }

    [Fact]
    public void Catalog_MarksExactlyTheAdministrativePermissionsAsProtected()
    {
        var expectedProtectedCodes = new[]
        {
            "users.view",
            "users.create",
            "users.status.manage",
            "users.roles.manage",
            "roles.view",
            "roles.permissions.manage",
            "permissions.view"
        };

        var protectedCodes = _catalog.GetAll()
            .Where(definition => definition.IsProtectedForAdministrator)
            .Select(definition => definition.Code)
            .Order();

        Assert.Equal(expectedProtectedCodes.Order(), protectedCodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Patients.view")]
    [InlineData("patients..view")]
    public void PermissionDefinition_WithInvalidCode_ThrowsArgumentException(string code)
    {
        Assert.Throws<ArgumentException>(() => new PermissionDefinition(
            code, "View patients", null, "Patients", false));
    }

    [Fact]
    public void PermissionDefinition_WithValidCatalogCode_IsAccepted()
    {
        var definition = Assert.IsType<PermissionDefinition>(_catalog.FindByCode("patients.view"));

        Assert.Equal("patients.view", definition.Code);
        Assert.False(definition.IsProtectedForAdministrator);
    }
}
