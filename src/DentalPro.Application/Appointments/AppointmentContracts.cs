using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public static class AppointmentPermissionCodes
{
    public const string View = PermissionCatalog.AppointmentsView;
    public const string ViewAll = PermissionCatalog.AppointmentsViewAll;
    public const string Create = PermissionCatalog.AppointmentsCreate;
    public const string Update = PermissionCatalog.AppointmentsUpdate;
    public const string Cancel = PermissionCatalog.AppointmentsCancel;
    public const string Confirm = PermissionCatalog.AppointmentsConfirm;
    public const string Complete = PermissionCatalog.AppointmentsComplete;
    public const string ScheduleManage = PermissionCatalog.AppointmentsScheduleManage;
}

public sealed record AppointmentQuery(DateTimeOffset From, DateTimeOffset To, Guid? DentistId = null);

public sealed record AppointmentAvailabilityQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid DentistId,
    Guid? PatientId = null);

public sealed record AppointmentInterval(DateTimeOffset Start, DateTimeOffset End);

public sealed record AppointmentAvailability(
    IReadOnlyList<AppointmentInterval> AvailableIntervals,
    int DefaultAppointmentDurationMinutes = 0,
    int StartIncrementMinutes = 0);

public sealed record AppointmentAuditEntry(
    Guid ActorId,
    string Action,
    Guid AppointmentId,
    Guid? RelatedAppointmentId,
    DateTimeOffset OccurredAt);

internal sealed class AppointmentScopeResolver(IEffectivePermissionQuery permissions)
{
    public async Task EnsurePermissionAsync(Guid actorId, string permissionCode, CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (!await permissions.HasPermissionAsync(actorId, permissionCode, cancellationToken))
            throw new AppointmentOutOfScopeException();
    }

    public async Task<AppointmentScope> ResolveAsync(
        Guid actorId,
        Guid? requestedDentistId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actorId);
        if (!await permissions.HasPermissionAsync(actorId, AppointmentPermissionCodes.View, cancellationToken))
        {
            throw new AppointmentOutOfScopeException();
        }

        var hasAllScope = await permissions.HasPermissionAsync(
            actorId, AppointmentPermissionCodes.ViewAll, cancellationToken);

        return hasAllScope
            ? new AppointmentScope(requestedDentistId, false)
            : new AppointmentScope(actorId, requestedDentistId.HasValue && requestedDentistId != actorId);
    }

    public async Task EnsureCanManageDentistAsync(
        Guid actorId,
        Guid dentistId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(actorId, dentistId, cancellationToken);
        if (scope.IsEmpty || (scope.DentistId.HasValue && scope.DentistId != dentistId))
        {
            throw new AppointmentOutOfScopeException();
        }
    }

    public static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new AppointmentOutOfScopeException();
        }
    }
}

internal sealed record AppointmentScope(Guid? DentistId, bool IsEmpty);
