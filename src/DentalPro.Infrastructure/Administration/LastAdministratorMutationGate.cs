namespace DentalPro.Infrastructure.Administration;

/// <summary>
/// Serializes last-administrator changes within one API process. PostgreSQL
/// serializable transactions provide the corresponding cross-process guard.
/// </summary>
internal static class LastAdministratorMutationGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                Gate.Release();
            }
        }
    }
}
