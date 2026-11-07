namespace DentalPro.Application.AccessControl;

/// <summary>
/// Provides the single code-defined catalog of permissions supported by the backend.
/// </summary>
public interface IPermissionCatalog
{
    IReadOnlyCollection<PermissionDefinition> GetAll();

    PermissionDefinition? FindByCode(string code);
}
