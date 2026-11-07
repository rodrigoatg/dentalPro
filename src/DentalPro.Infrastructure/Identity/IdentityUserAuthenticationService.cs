using DentalPro.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace DentalPro.Infrastructure.Identity;

public sealed class IdentityUserAuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenGenerator tokenGenerator) : IUserAuthenticationService
{
    public async Task<AuthenticationAttempt> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return AuthenticationAttempt.Rejected(AuthenticationFailure.InvalidCredentials);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return AuthenticationAttempt.Rejected(signInResult.IsLockedOut
                ? AuthenticationFailure.LockedOut
                : AuthenticationFailure.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return AuthenticationAttempt.Rejected(AuthenticationFailure.AccountDisabled);
        }

        var roles = (await userManager.GetRolesAsync(user))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var token = tokenGenerator.Create(user, roles);
        var authenticatedSession = new AuthenticatedSession(
            user.Id,
            user.UserName ?? user.Id.ToString(),
            roles,
            token.Value,
            token.ExpiresAt);

        return AuthenticationAttempt.Success(authenticatedSession);
    }
}
