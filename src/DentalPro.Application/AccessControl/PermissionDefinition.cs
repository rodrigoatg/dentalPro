using DentalPro.Domain.AccessControl;

namespace DentalPro.Application.AccessControl;

/// <summary>
/// Immutable code-defined metadata for a permission supported by the backend.
/// </summary>
public sealed record PermissionDefinition
{
    public PermissionDefinition(
        string code,
        string name,
        string? description,
        string module,
        bool isProtectedForAdministrator)
    {
        Code = PermissionCode.Create(code).Value;
        Name = EnsureRequired(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Module = EnsureRequired(module, nameof(module));
        IsProtectedForAdministrator = isProtectedForAdministrator;
    }

    public string Code { get; }

    public string Name { get; }

    public string? Description { get; }

    public string Module { get; }

    public bool IsProtectedForAdministrator { get; }

    private static string EnsureRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
