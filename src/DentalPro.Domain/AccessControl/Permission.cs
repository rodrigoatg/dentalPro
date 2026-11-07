namespace DentalPro.Domain.AccessControl;

/// <summary>
/// A backend capability that may be assigned to a role.
/// </summary>
public sealed class Permission
{
    private Permission(Guid id, string code, string name, string? description, string module)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
        Module = module;
    }

    public Guid Id { get; }

    /// <summary>
    /// Stable technical identifier used for authorization.
    /// </summary>
    public string Code { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string Module { get; private set; }

    public static Permission Create(string code, string name, string? description, string module)
    {
        var permissionCode = PermissionCode.Create(code);
        EnsureRequiredMetadata(name, nameof(name));
        EnsureRequiredMetadata(module, nameof(module));

        return new Permission(
            Guid.NewGuid(), permissionCode.Value, name.Trim(), NormalizeOptional(description), module.Trim());
    }

    /// <summary>
    /// Changes descriptive metadata without changing the authorization code.
    /// </summary>
    public void UpdateMetadata(string name, string? description, string module)
    {
        EnsureRequiredMetadata(name, nameof(name));
        EnsureRequiredMetadata(module, nameof(module));

        Name = name.Trim();
        Description = NormalizeOptional(description);
        Module = module.Trim();
    }

    private static void EnsureRequiredMetadata(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
