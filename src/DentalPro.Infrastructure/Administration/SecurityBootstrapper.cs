using System.Data;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Administration;
using DentalPro.Domain.AccessControl;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DentalPro.Infrastructure.Administration;

public sealed class SecurityBootstrapper(
    DentalProDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionCatalog catalog,
    IOptions<SecurityBootstrapOptions> options) : ISecurityBootstrapper
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        foreach (var roleName in SystemRoles.All)
        {
            if (await roleManager.FindByNameAsync(roleName) is null)
            {
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)
                {
                    Id = Guid.NewGuid(),
                    NormalizedName = roleManager.NormalizeKey(roleName)
                }));
            }
        }

        var permissionsByCode = await dbContext.Permissions.ToDictionaryAsync(
            permission => permission.Code, StringComparer.Ordinal, cancellationToken);
        foreach (var definition in catalog.GetAll())
        {
            if (permissionsByCode.TryGetValue(definition.Code, out var permission))
            {
                if (permission.Name != definition.Name || permission.Description != definition.Description ||
                    permission.Module != definition.Module)
                {
                    permission.UpdateMetadata(definition.Name, definition.Description, definition.Module);
                }
            }
            else
            {
                permission = Permission.Create(
                    definition.Code, definition.Name, definition.Description, definition.Module);
                dbContext.Permissions.Add(permission);
                permissionsByCode.Add(definition.Code, permission);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var roleIds = await dbContext.Roles
            .Where(role => role.Name != null && SystemRoles.All.Contains(role.Name))
            .ToDictionaryAsync(role => role.Name!, role => role.Id, StringComparer.Ordinal, cancellationToken);
        var desired = new HashSet<(Guid RoleId, Guid PermissionId)>();
        var initialAssignments = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [SystemRoles.Administrator] =
            [
                PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsViewAll,
                PermissionCatalog.AppointmentsCreate, PermissionCatalog.AppointmentsUpdate,
                PermissionCatalog.AppointmentsCancel, PermissionCatalog.AppointmentsConfirm,
                PermissionCatalog.AppointmentsComplete, PermissionCatalog.AppointmentsScheduleManage
            ],
            [SystemRoles.Receptionist] =
            [
                PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsViewAll,
                PermissionCatalog.AppointmentsCreate, PermissionCatalog.AppointmentsUpdate,
                PermissionCatalog.AppointmentsCancel, PermissionCatalog.AppointmentsConfirm,
                PermissionCatalog.AppointmentsComplete
            ],
            [SystemRoles.Dentist] =
            [
                PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsConfirm,
                PermissionCatalog.AppointmentsComplete, PermissionCatalog.ClinicalRecordsView,
                PermissionCatalog.ClinicalEntriesCreate, PermissionCatalog.TreatmentsCreate
            ]
        };
        foreach (var roleName in SystemRoles.All)
        {
            foreach (var code in new[] { PermissionCatalog.PatientsView, PermissionCatalog.PatientsCreate,
                             PermissionCatalog.PatientsUpdate }
                         .Concat(initialAssignments[roleName]))
            {
                desired.Add((roleIds[roleName], permissionsByCode[code].Id));
            }
        }

        foreach (var definition in catalog.GetAll().Where(permission => permission.IsProtectedForAdministrator))
        {
            desired.Add((roleIds[SystemRoles.Administrator], permissionsByCode[definition.Code].Id));
        }

        var existing = await dbContext.RolePermissions.AsNoTracking()
            .Select(assignment => new { assignment.RoleId, assignment.PermissionId })
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(assignment => (assignment.RoleId, assignment.PermissionId)).ToHashSet();
        foreach (var (roleId, permissionId) in desired.Except(existingKeys))
        {
            dbContext.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureInitialAdministratorAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureInitialAdministratorAsync(CancellationToken cancellationToken)
    {
        var administratorRole = await roleManager.FindByNameAsync(SystemRoles.Administrator)
            ?? throw new InvalidOperationException("Administrator role was not initialized.");
        var existingAdministrator = await userManager.GetUsersInRoleAsync(SystemRoles.Administrator);
        if (existingAdministrator.Count > 0)
        {
            return;
        }

        var bootstrap = options.Value;
        if (string.IsNullOrWhiteSpace(bootstrap.UserName) ||
            string.IsNullOrWhiteSpace(bootstrap.Password) ||
            string.IsNullOrWhiteSpace(bootstrap.FirstName) ||
            string.IsNullOrWhiteSpace(bootstrap.LastName))
        {
            throw new InvalidOperationException(
                "No Administrator exists. Configure SecurityBootstrap:InitialAdministrator values using an external secret provider before bootstrapping.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = new ApplicationUser
        {
            UserName = bootstrap.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(bootstrap.Email) ? null : bootstrap.Email.Trim(),
            FirstName = bootstrap.FirstName.Trim(),
            LastName = bootstrap.LastName.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        EnsureSuccess(await userManager.CreateAsync(user, bootstrap.Password));
        EnsureSuccess(await userManager.AddToRoleAsync(user, administratorRole.Name!));
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Security bootstrap failed Identity validation: " +
                string.Join(" ", result.Errors.Select(error => error.Code)));
        }
    }
}
