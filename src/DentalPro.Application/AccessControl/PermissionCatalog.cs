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
    public const string AppointmentsView = "appointments.view";
    public const string AppointmentsViewAll = "appointments.view.all";
    public const string AppointmentsCreate = "appointments.create";
    public const string AppointmentsUpdate = "appointments.update";
    public const string AppointmentsCancel = "appointments.cancel";
    public const string AppointmentsConfirm = "appointments.confirm";
    public const string AppointmentsComplete = "appointments.complete";
    public const string AppointmentsScheduleManage = "appointments.schedule.manage";

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
        new(PermissionsView, "View permissions", "View the backend permission catalog.", "Identity", true),
        new(AppointmentsView, "View own agenda", "View the authenticated Dentist's agenda.", "Appointments", false),
        new(AppointmentsViewAll, "View general agenda", "Expand agenda scope to all Dentists.", "Appointments", false),
        new(AppointmentsCreate, "Create appointments", "Create appointments.", "Appointments", false),
        new(AppointmentsUpdate, "Reschedule appointments", "Reschedule appointments.", "Appointments", false),
        new(AppointmentsCancel, "Cancel appointments", "Cancel appointments.", "Appointments", false),
        new(AppointmentsConfirm, "Confirm appointments", "Confirm appointments.", "Appointments", false),
        new(AppointmentsComplete, "Complete appointments", "Mark appointments as completed.", "Appointments", false),
        new(AppointmentsScheduleManage, "Manage clinic schedule", "Configure clinic hours and exceptions.", "Appointments", false)
    ]);

    public IReadOnlyCollection<PermissionDefinition> GetAll() => Definitions;

    public PermissionDefinition? FindByCode(string code) =>
        Definitions.SingleOrDefault(permission => string.Equals(permission.Code, code, StringComparison.Ordinal));

    public static bool IsDefined(string code) =>
        Definitions.Any(permission => string.Equals(permission.Code, code, StringComparison.Ordinal));
}
