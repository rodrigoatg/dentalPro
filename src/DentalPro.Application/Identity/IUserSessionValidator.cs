namespace DentalPro.Application.Identity;

/// <summary>
/// Validates that the user and security state associated with an access token are current.
/// </summary>
public interface IUserSessionValidator
{
    Task<bool> IsCurrentAsync(
        Guid userId,
        string securityStampFingerprint,
        CancellationToken cancellationToken = default);
}
