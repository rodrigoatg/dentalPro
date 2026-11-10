using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public sealed class ListAppointments(IAppointmentRepository repository, IEffectivePermissionQuery permissions)
{
    public async Task<IReadOnlyList<Appointment>> ExecuteAsync(
        Guid actorId,
        AppointmentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateRange(query.From, query.To);
        var scope = await new AppointmentScopeResolver(permissions)
            .ResolveAsync(actorId, query.DentistId, cancellationToken);
        if (scope.IsEmpty)
        {
            return [];
        }

        return await repository.ListAsync(
            query.From.ToUniversalTime(), query.To.ToUniversalTime(), scope.DentistId, cancellationToken);
    }

    internal static void ValidateRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from >= to)
        {
            throw new AppointmentQueryValidationException("The start of the query interval must be before its end.");
        }
    }
}
