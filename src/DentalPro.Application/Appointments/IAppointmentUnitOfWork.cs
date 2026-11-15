namespace DentalPro.Application.Appointments;

/// <summary>
/// Commits staged scheduling writes (appointments, clinic configuration and audit) together only if the callback succeeds.
/// PostgreSQL exclusion constraints may be deferred until the transaction commit.
/// </summary>
public interface IAppointmentUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
