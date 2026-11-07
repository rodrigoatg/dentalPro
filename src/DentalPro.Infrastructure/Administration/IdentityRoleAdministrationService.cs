using System.Data;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Administration;
using DentalPro.Domain.AccessControl;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.Administration;

public sealed class IdentityRoleAdministrationService(
    DentalProDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    IPermissionCatalog catalog) : IRoleAdministrationService
{
    public async Task<IReadOnlyList<RoleSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Roles.AsNoTracking()
            .Where(role => role.Name != null && SystemRoles.All.Contains(role.Name))
            .OrderBy(role => role.Name)
            .Select(role => new RoleSummary(role.Id, role.Name!))
            .ToListAsync(cancellationToken);

    public async Task<RolePermissionSummary?> GetPermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role?.Name is null || !SystemRoles.All.Contains(role.Name, StringComparer.Ordinal))
        {
            return null;
        }

        var assigned = await (
                from assignment in dbContext.RolePermissions.AsNoTracking()
                join permission in dbContext.Permissions.AsNoTracking()
                    on assignment.PermissionId equals permission.Id
                where assignment.RoleId == roleId
                select permission.Code)
            .ToListAsync(cancellationToken);

        return new RolePermissionSummary(
            roleId,
            role.Name,
            catalog.GetAll().OrderBy(permission => permission.Code, StringComparer.Ordinal).ToArray(),
            new HashSet<string>(assigned, StringComparer.Ordinal));
    }

    public async Task<RolePermissionSummary> SetPermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken = default)
    {
        var codes = permissionCodes.ToHashSet(StringComparer.Ordinal);
        var definitions = catalog.GetAll().ToDictionary(permission => permission.Code, StringComparer.Ordinal);
        var unknownCodes = codes.Where(code => !definitions.ContainsKey(code)).ToArray();
        if (unknownCodes.Length > 0)
        {
            throw new AdministrationValidationException(
                $"Permission code '{unknownCodes[0]}' is not included in the backend catalog.");
        }

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role?.Name is null || !SystemRoles.All.Contains(role.Name, StringComparer.Ordinal))
        {
            throw new AdministrationNotFoundException("Role was not found.");
        }

        var protectedCodes = definitions.Values
            .Where(permission => permission.IsProtectedForAdministrator)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        if (role.Name != SystemRoles.Administrator && codes.Overlaps(protectedCodes))
        {
            throw new AdministrationConflictException(
                "Protected administrative permissions can only be assigned to Administrator.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var persisted = await dbContext.Permissions
            .Where(permission => codes.Contains(permission.Code) ||
                (role.Name == SystemRoles.Administrator && protectedCodes.Contains(permission.Code)))
            .ToDictionaryAsync(permission => permission.Code, cancellationToken);

        var desiredCodes = new HashSet<string>(codes, StringComparer.Ordinal);
        if (role.Name == SystemRoles.Administrator)
        {
            // These permissions are code-protected and are forced back into the assignment set.
            desiredCodes.UnionWith(protectedCodes);
        }

        var desiredIds = desiredCodes.Select(code => persisted.TryGetValue(code, out var permission)
                ? permission.Id
                : throw new AdministrationConflictException(
                    $"Permission '{code}' has not been initialized in persistence."))
            .ToHashSet();
        var assignments = await dbContext.RolePermissions
            .Where(assignment => assignment.RoleId == roleId)
            .ToListAsync(cancellationToken);
        var desiredPermissionIds = desiredIds;
        dbContext.RolePermissions.RemoveRange(assignments.Where(assignment =>
            !desiredPermissionIds.Contains(assignment.PermissionId)));
        var currentIds = assignments.Select(assignment => assignment.PermissionId).ToHashSet();
        foreach (var permissionId in desiredPermissionIds.Except(currentIds))
        {
            dbContext.RolePermissions.Add(RolePermission.Create(roleId, permissionId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetPermissionsAsync(roleId, cancellationToken))!;
    }
}
