using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public static class AppointmentPermissionCodes
{
    public const string View = "appointments.view";
    public const string ViewAll = "appointments.view.all";
}

public sealed record AppointmentQuery(DateTimeOffset From, DateTimeOffset To, Guid? DentistId = null);

public sealed record AppointmentAvailabilityQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid DentistId,
    Guid? PatientId = null);

public sealed record AppointmentInterval(DateTimeOffset Start, DateTimeOffset End);

public sealed record AppointmentAvailability(IReadOnlyList<AppointmentInterval> AvailableIntervals);

public sealed record AppointmentAuditEntry(
    Guid ActorId,
    string Action,
    Guid AppointmentId,
    Guid? RelatedAppointmentId,
    DateTimeOffset OccurredAt);

internal sealed class AppointmentScopeResolver(IEffectivePermissionQuery permissions)
{
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

    public async Task EnsureOwnsAsync(
        Guid actorId,
        Appointment appointment,
        bool ownerOnly,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        var scope = await ResolveAsync(actorId, ownerOnly ? actorId : appointment.DentistId, cancellationToken);
        if (appointment.DentistId != actorId && (ownerOnly || !scope.DentistId.HasValue))
        {
            throw new AppointmentOutOfScopeException();
        }

        if (!ownerOnly && scope.DentistId.HasValue && appointment.DentistId != scope.DentistId)
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
