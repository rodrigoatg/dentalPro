namespace DentalPro.Application.AccessControl;

/// <summary>
/// The canonical catalog of permissions supported by the current backend.
/// </summary>
public sealed class PermissionCatalog : IPermissionCatalog
{
    public const string PatientsView = "patients.view";
    public const string PatientsCreate = "patients.create";
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersStatusManage = "users.status.manage";
    public const string UsersRolesManage = "users.roles.manage";
    public const string RolesView = "roles.view";
    public const string RolesPermissionsManage = "roles.permissions.manage";
    public const string PermissionsView = "permissions.view";

    private static readonly IReadOnlyCollection<PermissionDefinition> Definitions =
        Array.AsReadOnly<PermissionDefinition>(
    [
        new(PatientsView, "View patients", "List patients.", "Patients", false),
        new(PatientsCreate, "Create patients", "Register patients.", "Patients", false),
        new(UsersView, "View users", "List and inspect user accounts.", "Identity", true),
        new(UsersCreate, "Create users", "Create user accounts.", "Identity", true),
        new(UsersStatusManage, "Manage user status", "Activate or deactivate user accounts.", "Identity", true),
        new(UsersRolesManage, "Manage user roles", "Assign or remove roles from users.", "Identity", true),
        new(RolesView, "View roles", "List roles and inspect their permission assignments.", "Identity", true),
        new(RolesPermissionsManage, "Manage role permissions", "Assign or remove permissions on roles.", "Identity", true),
        new(PermissionsView, "View permissions", "View the backend permission catalog.", "Identity", true)
    ]);

    public IReadOnlyCollection<PermissionDefinition> GetAll() => Definitions;

    public PermissionDefinition? FindByCode(string code) =>
        Definitions.SingleOrDefault(permission => string.Equals(permission.Code, code, StringComparison.Ordinal));

    public static bool IsDefined(string code) =>
        Definitions.Any(permission => string.Equals(permission.Code, code, StringComparison.Ordinal));
}
