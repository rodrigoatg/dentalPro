namespace DentalPro.Infrastructure.Administration;

public sealed class SecurityBootstrapOptions
{
    public const string SectionName = "SecurityBootstrap:InitialAdministrator";

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Password { get; set; }
}
