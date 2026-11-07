namespace DentalPro.Domain.AccessControl;

/// <summary>
/// A stable, lowercase, dot-separated permission identifier.
/// </summary>
public readonly record struct PermissionCode
{
    private PermissionCode(string value) => Value = value;

    public string Value { get; }

    public static PermissionCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.')) ||
            value.StartsWith('.') ||
            value.EndsWith('.') ||
            value.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Permission code must be a lowercase, dot-separated technical identifier.",
                nameof(value));
        }

        return new PermissionCode(value);
    }
}
