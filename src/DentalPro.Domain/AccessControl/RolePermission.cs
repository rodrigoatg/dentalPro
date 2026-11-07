namespace DentalPro.Domain.AccessControl;

/// <summary>
/// Assigns one permission to one role.
/// </summary>
public sealed class RolePermission
{
    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    /// <summary>
    /// Opaque role identifier. It is a Guid so it can reference an
    /// ASP.NET Core Identity role without depending on Identity types.
    /// </summary>
    public Guid RoleId { get; }

    public Guid PermissionId { get; }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role is required.", nameof(roleId));
        }

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission is required.", nameof(permissionId));
        }

        return new RolePermission(roleId, permissionId);
    }
}
