using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentalPro.Api.Authorization;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Administration;

namespace DentalPro.Api.Administration;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/me", GetCurrentAsync)
            .RequireAuthorization();

        var admin = endpoints.MapGroup("/api/admin")
            .AddEndpointFilter<AdministrationExceptionEndpointFilter>();

        admin.MapGet("/users", ListUsersAsync)
            .RequirePermission(PermissionCatalog.UsersView);
        admin.MapPost("/users", CreateUserAsync)
            .RequirePermission(PermissionCatalog.UsersCreate);
        admin.MapPut("/users/{id:guid}/roles", SetUserRolesAsync)
            .RequirePermission(PermissionCatalog.UsersRolesManage);
        admin.MapPatch("/users/{id:guid}/status", SetUserStatusAsync)
            .RequirePermission(PermissionCatalog.UsersStatusManage);
        admin.MapGet("/roles", ListRolesAsync)
            .RequirePermission(PermissionCatalog.RolesView);
        admin.MapGet("/roles/{id:guid}/permissions", GetRolePermissionsAsync)
            .RequirePermission(PermissionCatalog.RolesView);
        admin.MapPut("/roles/{id:guid}/permissions", SetRolePermissionsAsync)
            .RequirePermission(PermissionCatalog.RolesPermissionsManage);
        admin.MapGet("/permissions", (IPermissionCatalog catalog) =>
                Results.Ok(catalog.GetAll().OrderBy(permission => permission.Code, StringComparer.Ordinal)))
            .RequirePermission(PermissionCatalog.PermissionsView);

        return endpoints;
    }

    private static async Task<IResult> GetCurrentAsync(
        ClaimsPrincipal principal,
        IUserAdministrationService users,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var current = await users.GetCurrentAsync(userId, cancellationToken);
        return current is null ? Results.Unauthorized() : Results.Ok(current);
    }

    private static async Task<IResult> ListUsersAsync(
        IUserAdministrationService users,
        CancellationToken cancellationToken) =>
        Results.Ok(await users.ListAsync(cancellationToken));

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        IUserAdministrationService users,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UserName)) errors[nameof(request.UserName)] = ["Username is required."];
        if (string.IsNullOrWhiteSpace(request.FirstName)) errors[nameof(request.FirstName)] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(request.LastName)) errors[nameof(request.LastName)] = ["Last name is required."];
        if (string.IsNullOrWhiteSpace(request.Password)) errors[nameof(request.Password)] = ["Password is required."];
        if (request.Roles is null) errors[nameof(request.Roles)] = ["Roles are required."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var created = await users.CreateAsync(new CreateUserInput(
            request.UserName!, request.Email, request.FirstName!, request.LastName!, request.Password!, request.Roles!),
            cancellationToken);
        return Results.Json(created, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> SetUserRolesAsync(
        Guid id,
        SetUserRolesRequest request,
        ClaimsPrincipal principal,
        IUserAdministrationService users,
        CancellationToken cancellationToken)
    {
        if (request.Roles is null) return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Roles)] = ["Roles are required."]
        });
        if (!TryGetUserId(principal, out var actorId)) return Results.Unauthorized();
        return Results.Ok(await users.SetRolesAsync(actorId, id, request.Roles, cancellationToken));
    }

    private static async Task<IResult> SetUserStatusAsync(
        Guid id,
        SetUserStatusRequest request,
        ClaimsPrincipal principal,
        IUserAdministrationService users,
        CancellationToken cancellationToken)
    {
        if (request.IsActive is null) return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.IsActive)] = ["IsActive is required."]
        });
        if (!TryGetUserId(principal, out var actorId)) return Results.Unauthorized();
        return Results.Ok(await users.SetStatusAsync(actorId, id, request.IsActive.Value, cancellationToken));
    }

    private static async Task<IResult> ListRolesAsync(
        IRoleAdministrationService roles,
        CancellationToken cancellationToken) =>
        Results.Ok(await roles.ListAsync(cancellationToken));

    private static async Task<IResult> GetRolePermissionsAsync(
        Guid id,
        IRoleAdministrationService roles,
        CancellationToken cancellationToken)
    {
        var result = await roles.GetPermissionsAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> SetRolePermissionsAsync(
        Guid id,
        SetRolePermissionsRequest request,
        IRoleAdministrationService roles,
        CancellationToken cancellationToken)
    {
        if (request.PermissionCodes is null) return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.PermissionCodes)] = ["Permission codes are required."]
        });
        return Results.Ok(await roles.SetPermissionsAsync(id, request.PermissionCodes, cancellationToken));
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId);
}
