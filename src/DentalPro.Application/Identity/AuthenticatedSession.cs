namespace DentalPro.Application.Identity;

public sealed record AuthenticatedSession(
    Guid UserId,
    string UserName,
    IReadOnlyList<string> Roles,
    string AccessToken,
    DateTimeOffset ExpiresAt);
