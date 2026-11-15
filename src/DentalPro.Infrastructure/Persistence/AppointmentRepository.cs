using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DentalPro.Infrastructure.Persistence;

public sealed class AppointmentRepository(DentalProDbContext dbContext) : IAppointmentRepository
{
    private readonly Dictionary<Guid, TrackedAppointment> _trackedAppointments = [];

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? dentistId,
        CancellationToken cancellationToken = default)
    {
        var utcFrom = from.ToUniversalTime();
        var utcTo = to.ToUniversalTime();
        var rows = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.StartDateTime < utcTo && appointment.EndDateTime > utcFrom)
            .Where(appointment => !dentistId.HasValue || appointment.DentistId == dentistId.Value)
            .OrderBy(appointment => appointment.StartDateTime)
            .ThenBy(appointment => appointment.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDomain).ToArray();
    }

    public async Task<Appointment?> GetByIdForUpdateAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("GetByIdForUpdateAsync requires an active database transaction.");

        if (_trackedAppointments.TryGetValue(appointmentId, out var existing))
        {
            if (dbContext.Entry(existing.Row).State == EntityState.Detached)
            {
                _trackedAppointments.Remove(appointmentId);
            }
            else if (ReferenceEquals(existing.LockedByTransaction, transaction))
            {
                return existing.Aggregate;
            }
        }

        // NoTracking is intentional: PostgreSQL waits for a concurrent updater and returns
        // its committed row version, while EF tracking would otherwise keep stale values.
        var row = await dbContext.Appointments
            .FromSqlInterpolated($"SELECT * FROM \"Appointments\" WHERE \"Id\" = {appointmentId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            _trackedAppointments.Remove(appointmentId);
            return null;
        }

        AppointmentRow trackedRow;
        if (_trackedAppointments.TryGetValue(appointmentId, out existing)
            && dbContext.Entry(existing.Row).State != EntityState.Detached)
        {
            var entry = dbContext.Entry(existing.Row);
            if (entry.State != EntityState.Unchanged)
            {
                throw new InvalidOperationException(
                    "An appointment cannot be refreshed for update after local row changes have been staged.");
            }

            entry.CurrentValues.SetValues(row);
            entry.OriginalValues.SetValues(row);
            trackedRow = existing.Row;
        }
        else
        {
            trackedRow = await dbContext.Appointments.SingleAsync(
                appointment => appointment.Id == appointmentId,
                cancellationToken);
            var entry = dbContext.Entry(trackedRow);
            entry.CurrentValues.SetValues(row);
            entry.OriginalValues.SetValues(row);
        }

        var aggregate = Rehydrate(row);
        _trackedAppointments[appointmentId] = new TrackedAppointment(trackedRow, aggregate, transaction);
        return aggregate;
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_trackedAppointments.TryGetValue(appointment.Id, out var tracked)
            || !ReferenceEquals(tracked.Aggregate, appointment)
            || dbContext.Entry(tracked.Row).State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                "The appointment must be loaded by this repository before it can be updated.");
        }

        // Existing domain operations only change status and UpdatedAt on this appointment.
        // Its identifiers, interval, notes, rescheduling reference and CreatedAt are historical.
        tracked.Row.Status = appointment.Status;
        tracked.Row.UpdatedAt = appointment.UpdatedAt.ToUniversalTime();
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Appointment>> GetBlockingAsync(
        Guid dentistId,
        Guid? patientId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? excludingAppointmentId = null,
        CancellationToken cancellationToken = default)
    {
        var utcFrom = from.ToUniversalTime();
        var utcTo = to.ToUniversalTime();
        var blockingStatuses = new[]
        {
            AppointmentStatus.Scheduled,
            AppointmentStatus.Confirmed,
            AppointmentStatus.Completed
        };

        var rows = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => !excludingAppointmentId.HasValue
                || appointment.Id != excludingAppointmentId.Value)
            .Where(appointment => appointment.DentistId == dentistId
                || (patientId.HasValue && appointment.PatientId == patientId.Value))
            .Where(appointment => blockingStatuses.Contains(appointment.Status))
            .Where(appointment => appointment.StartDateTime < utcTo && appointment.EndDateTime > utcFrom)
            .OrderBy(appointment => appointment.StartDateTime)
            .ThenBy(appointment => appointment.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDomain).ToArray();
    }

    public void Add(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        if (dbContext.Appointments.Local.Any(row => row.Id == appointment.Id))
        {
            throw new InvalidOperationException($"An appointment with ID '{appointment.Id}' is already tracked.");
        }

        dbContext.Appointments.Add(ToRow(appointment));
    }

    private Appointment ToDomain(AppointmentRow row)
    {
        if (_trackedAppointments.TryGetValue(row.Id, out var tracked))
        {
            if (dbContext.Entry(tracked.Row).State != EntityState.Detached)
            {
                return tracked.Aggregate;
            }

            _trackedAppointments.Remove(row.Id);
        }

        return Rehydrate(row);
    }

    private static Appointment Rehydrate(AppointmentRow row) =>
        Appointment.Rehydrate(
            row.Id,
            row.PatientId,
            row.DentistId,
            row.StartDateTime,
            row.EndDateTime,
            row.DurationMinutes,
            row.Status,
            row.Notes,
            row.RescheduledFromAppointmentId,
            row.CreatedAt,
            row.UpdatedAt);

    private static AppointmentRow ToRow(Appointment appointment) => new()
    {
        Id = appointment.Id,
        PatientId = appointment.PatientId,
        DentistId = appointment.DentistId,
        StartDateTime = appointment.StartDateTime.ToUniversalTime(),
        EndDateTime = appointment.EndDateTime.ToUniversalTime(),
        DurationMinutes = appointment.DurationMinutes,
        Status = appointment.Status,
        Notes = appointment.Notes,
        RescheduledFromAppointmentId = appointment.RescheduledFromAppointmentId,
        CreatedAt = appointment.CreatedAt.ToUniversalTime(),
        UpdatedAt = appointment.UpdatedAt.ToUniversalTime()
    };

    private sealed record TrackedAppointment(
        AppointmentRow Row,
        Appointment Aggregate,
        IDbContextTransaction? LockedByTransaction = null);
}
