namespace DentalPro.Application.Identity;

public interface IUserAuthenticationService
{
    Task<AuthenticationAttempt> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}
