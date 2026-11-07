using DentalPro.Domain.AccessControl;
using DentalPro.Infrastructure.AccessControl;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.IntegrationTests;

public sealed class EffectivePermissionQueryTests
{
    [Fact]
    public async Task GetForUserAsync_ReturnsDistinctUnionAcrossCurrentRoles()
    {
        var userId = Guid.NewGuid();
        var dentistRoleId = Guid.NewGuid();
        var receptionistRoleId = Guid.NewGuid();
        var viewPatients = Permission.Create("patients.view", "View patients", null, "Patients");
        var createPatients = Permission.Create("patients.create", "Create patients", null, "Patients");

        var options = new DbContextOptionsBuilder<DentalProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DentalProDbContext(options);
        dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "rbac-user" });
        dbContext.Roles.AddRange(
            new IdentityRole<Guid> { Id = dentistRoleId, Name = "Dentist", NormalizedName = "DENTIST" },
            new IdentityRole<Guid> { Id = receptionistRoleId, Name = "Receptionist", NormalizedName = "RECEPTIONIST" });
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = userId, RoleId = dentistRoleId },
            new IdentityUserRole<Guid> { UserId = userId, RoleId = receptionistRoleId });
        dbContext.Permissions.AddRange(viewPatients, createPatients);
        dbContext.RolePermissions.AddRange(
            RolePermission.Create(dentistRoleId, viewPatients.Id),
            RolePermission.Create(receptionistRoleId, createPatients.Id),
            RolePermission.Create(receptionistRoleId, viewPatients.Id));
        await dbContext.SaveChangesAsync();

        var query = new EffectivePermissionQuery(dbContext);
        var effective = await query.GetForUserAsync(userId);

        Assert.Equal(2, effective.Count);
        Assert.Contains("patients.view", effective);
        Assert.Contains("patients.create", effective);
        Assert.True(await query.HasPermissionAsync(userId, "patients.view"));
    }

    [Fact]
    public async Task GetForUserAsync_ReflectsRolePermissionChangesOnNextQuery_AndReturnsEmptyForNoRoles()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permission = Permission.Create("patients.view", "View patients", null, "Patients");
        var options = new DbContextOptionsBuilder<DentalProDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DentalProDbContext(options);
        dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = "rbac-user" });
        dbContext.Roles.Add(new IdentityRole<Guid> { Id = roleId, Name = "Dentist", NormalizedName = "DENTIST" });
        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync();

        var query = new EffectivePermissionQuery(dbContext);
        Assert.Empty(await query.GetForUserAsync(userId));

        dbContext.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        dbContext.RolePermissions.Add(RolePermission.Create(roleId, permission.Id));
        await dbContext.SaveChangesAsync();
        Assert.Contains("patients.view", await query.GetForUserAsync(userId));

        dbContext.RolePermissions.Remove(await dbContext.RolePermissions.SingleAsync());
        await dbContext.SaveChangesAsync();
        Assert.Empty(await query.GetForUserAsync(userId));
    }
}
