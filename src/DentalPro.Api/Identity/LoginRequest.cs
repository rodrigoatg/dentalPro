namespace DentalPro.Api.Identity;

public sealed record LoginRequest(string? UserName, string? Password);
