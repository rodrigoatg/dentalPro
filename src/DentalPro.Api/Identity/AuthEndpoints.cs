using DentalPro.Application.Identity;
using Microsoft.AspNetCore.Authorization;

namespace DentalPro.Api.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUserAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.UserName)] = string.IsNullOrWhiteSpace(request.UserName)
                    ? ["Username is required."]
                    : [],
                [nameof(request.Password)] = string.IsNullOrWhiteSpace(request.Password)
                    ? ["Password is required."]
                    : []
            }.Where(item => item.Value.Length > 0).ToDictionary(item => item.Key, item => item.Value));
        }

        var attempt = await authenticationService.AuthenticateAsync(
            request.UserName.Trim(),
            request.Password,
            cancellationToken);

        if (attempt.Session is { } session)
        {
            return Results.Ok(new LoginResponse(
                session.UserId,
                session.UserName,
                session.Roles,
                session.AccessToken,
                session.ExpiresAt));
        }

        return attempt.Failure switch
        {
            AuthenticationFailure.AccountDisabled => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account disabled."),
            AuthenticationFailure.LockedOut => Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account temporarily locked."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid username or password.")
        };
    }
}
