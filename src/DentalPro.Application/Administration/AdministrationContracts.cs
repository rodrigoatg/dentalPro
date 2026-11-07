using DentalPro.Application.AccessControl;

namespace DentalPro.Application.Administration;

public sealed record UserSummary(
    Guid Id,
    string UserName,
    string? Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record CurrentUserSummary(
    Guid Id,
    string UserName,
    string? Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> EffectivePermissions);

public sealed record RoleSummary(Guid Id, string Name);

public sealed record RolePermissionSummary(
    Guid RoleId,
    string RoleName,
    IReadOnlyList<PermissionDefinition> Catalog,
    IReadOnlySet<string> AssignedCodes);

public sealed record CreateUserInput(
    string UserName,
    string? Email,
    string FirstName,
    string LastName,
    string Password,
    IReadOnlyCollection<string> Roles);

public interface IUserAdministrationService
{
    Task<CurrentUserSummary?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<UserSummary> CreateAsync(CreateUserInput input, CancellationToken cancellationToken = default);

    Task<UserSummary> SetRolesAsync(
        Guid actorId,
        Guid targetUserId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default);

    Task<UserSummary> SetStatusAsync(
        Guid actorId,
        Guid targetUserId,
        bool isActive,
        CancellationToken cancellationToken = default);
}

public interface IRoleAdministrationService
{
    Task<IReadOnlyList<RoleSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<RolePermissionSummary?> GetPermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<RolePermissionSummary> SetPermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken = default);
}

public interface ISecurityBootstrapper
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
