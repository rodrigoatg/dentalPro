namespace DentalPro.Application.ClinicalRecords;

public interface IClinicalUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
