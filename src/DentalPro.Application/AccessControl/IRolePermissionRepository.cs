using DentalPro.Domain.AccessControl;

namespace DentalPro.Application.AccessControl;

/// <summary>
/// Reads and changes permission assignments for roles.
/// </summary>
public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> ListByRoleIdAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task AddAsync(RolePermission assignment, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);
}
