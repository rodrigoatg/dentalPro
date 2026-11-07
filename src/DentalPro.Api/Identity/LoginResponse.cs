namespace DentalPro.Api.Identity;

public sealed record LoginResponse(
    Guid UserId,
    string UserName,
    IReadOnlyList<string> Roles,
    string AccessToken,
    DateTimeOffset ExpiresAt);
