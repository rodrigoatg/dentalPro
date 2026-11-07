using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DentalPro.Application.Identity;
using DentalPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DentalPro.Api.Authentication;

public static class JwtAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddDentalProJwtBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = "role"
                };
                bearerOptions.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var stampFingerprint = context.Principal?
                            .FindFirst(JwtTokenGenerator.SecurityStampFingerprintClaim)?.Value;

                        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(stampFingerprint))
                        {
                            context.Fail("The access token is missing required identity state.");
                            return;
                        }

                        try
                        {
                            var validator = context.HttpContext.RequestServices
                                .GetRequiredService<IUserSessionValidator>();
                            if (!await validator.IsCurrentAsync(
                                    userId,
                                    stampFingerprint,
                                    context.HttpContext.RequestAborted))
                            {
                                context.Fail("The access token is no longer valid.");
                            }
                        }
                        catch
                        {
                            context.Fail("The current user state could not be verified.");
                        }
                    }
                };
            });

        return services;
    }
}
