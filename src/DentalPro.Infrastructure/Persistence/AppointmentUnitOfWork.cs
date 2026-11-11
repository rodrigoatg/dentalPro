using DentalPro.Application.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DentalPro.Infrastructure.Persistence;

public sealed class AppointmentUnitOfWork(
    DentalProDbContext dbContext,
    ILogger<AppointmentUnitOfWork> logger) : IAppointmentUnitOfWork
{
    private int _isExecuting;

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
        {
            throw new InvalidOperationException("Appointment unit-of-work operations cannot be nested or concurrent.");
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception)
            {
                await RollbackSafelyAsync(transaction);
                dbContext.ChangeTracker.Clear();

                var postgresException = FindPostgresException(exception);
                if (postgresException?.SqlState == PostgresErrorCodes.ExclusionViolation)
                {
                    logger.LogWarning(
                        "Appointment overlap rejected by PostgreSQL exclusion constraint {ConstraintName} (SQLSTATE {SqlState}).",
                        postgresException.ConstraintName,
                        postgresException.SqlState);
                    throw new AppointmentAvailabilityConflictException();
                }

                throw;
            }
        }
        finally
        {
            Volatile.Write(ref _isExecuting, 0);
        }
    }

    private async Task RollbackSafelyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            logger.LogError(rollbackException, "Failed to roll back the appointment unit-of-work transaction.");
        }
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        if (exception is PostgresException postgresException)
        {
            return postgresException;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                var found = FindPostgresException(innerException);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return exception.InnerException is null ? null : FindPostgresException(exception.InnerException);
    }
}
