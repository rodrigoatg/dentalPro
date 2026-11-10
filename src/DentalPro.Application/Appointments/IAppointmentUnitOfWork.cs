namespace DentalPro.Application.Appointments;

/// <summary>Commits staged appointment and audit writes together only if the callback succeeds.</summary>
public interface IAppointmentUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
