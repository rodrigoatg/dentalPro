using DentalPro.Application.AccessControl;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.AccessControl;

/// <summary>
/// Resolves permissions from persisted user-role and role-permission assignments on every call.
/// </summary>
public sealed class EffectivePermissionQuery(DentalProDbContext dbContext) : IEffectivePermissionQuery
{
    public async Task<IReadOnlySet<string>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var codes = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join rolePermission in dbContext.RolePermissions.AsNoTracking()
                    on userRole.RoleId equals rolePermission.RoleId
                join permission in dbContext.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where userRole.UserId == userId
                select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(codes, StringComparer.Ordinal);
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        return await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join rolePermission in dbContext.RolePermissions.AsNoTracking()
                    on userRole.RoleId equals rolePermission.RoleId
                join permission in dbContext.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where userRole.UserId == userId && permission.Code == permissionCode
                select permission.Id)
            .AnyAsync(cancellationToken);
    }
}
