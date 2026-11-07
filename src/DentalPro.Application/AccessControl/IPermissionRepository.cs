using DentalPro.Domain.AccessControl;

namespace DentalPro.Application.AccessControl;

/// <summary>
/// Reads the persisted permission catalog entries by their stable code.
/// </summary>
public interface IPermissionRepository
{
    Task<Permission?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> ListAsync(CancellationToken cancellationToken = default);
}
