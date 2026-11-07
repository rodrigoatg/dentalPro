using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DentalPro.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DentalPro.Infrastructure.Identity;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    public const string SecurityStampFingerprintClaim = "dentalpro:security_stamp";

    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken Create(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            throw new InvalidOperationException("The user has no security stamp.");
        }

        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(SecurityStampFingerprintClaim, CreateSecurityStampFingerprint(user.SecurityStamp))
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            issuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }

    public static string CreateSecurityStampFingerprint(string securityStamp) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp)));
}

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);
