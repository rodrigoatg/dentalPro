namespace DentalPro.Api.Administration;

public sealed record CreateUserRequest(
    string? UserName,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password,
    IReadOnlyList<string>? Roles);

public sealed record SetUserRolesRequest(IReadOnlyList<string>? Roles);

public sealed record SetUserStatusRequest(bool? IsActive);

public sealed record SetRolePermissionsRequest(IReadOnlyList<string>? PermissionCodes);
