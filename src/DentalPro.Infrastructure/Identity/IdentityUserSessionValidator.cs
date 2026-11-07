using System.Security.Cryptography;
using DentalPro.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace DentalPro.Infrastructure.Identity;

public sealed class IdentityUserSessionValidator(UserManager<ApplicationUser> userManager)
    : IUserSessionValidator
{
    public async Task<bool> IsCurrentAsync(
        Guid userId,
        string securityStampFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(securityStampFingerprint))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return false;
        }

        var currentStamp = await userManager.GetSecurityStampAsync(user);
        if (string.IsNullOrWhiteSpace(currentStamp))
        {
            return false;
        }

        var currentFingerprint = Convert.FromHexString(
            JwtTokenGenerator.CreateSecurityStampFingerprint(currentStamp));
        byte[] tokenFingerprint;
        try
        {
            tokenFingerprint = Convert.FromHexString(securityStampFingerprint);
        }
        catch (FormatException)
        {
            return false;
        }

        return tokenFingerprint.Length == currentFingerprint.Length &&
            CryptographicOperations.FixedTimeEquals(tokenFingerprint, currentFingerprint);
    }
}
