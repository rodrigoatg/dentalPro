using System.IdentityModel.Tokens.Jwt;
using DentalPro.Application.AccessControl;
using Microsoft.AspNetCore.Authorization;

namespace DentalPro.Api.Authorization;

public sealed class PermissionAuthorizationHandler(IEffectivePermissionQuery permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(userId, requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
