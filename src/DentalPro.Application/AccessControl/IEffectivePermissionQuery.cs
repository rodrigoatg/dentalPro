namespace DentalPro.Application.AccessControl;

/// <summary>
/// Queries permissions derived from a user's current role assignments.
/// </summary>
public interface IEffectivePermissionQuery
{
    Task<IReadOnlySet<string>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
