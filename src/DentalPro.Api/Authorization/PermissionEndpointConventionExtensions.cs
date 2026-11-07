using DentalPro.Application.AccessControl;
using Microsoft.AspNetCore.Builder;

namespace DentalPro.Api.Authorization;

public static class PermissionEndpointConventionExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder endpoint, string permissionCode)
    {
        if (!PermissionCatalog.IsDefined(permissionCode))
        {
            throw new ArgumentException($"Permission code '{permissionCode}' is not defined in the catalog.", nameof(permissionCode));
        }

        return endpoint.RequireAuthorization(PermissionPolicyProvider.PolicyPrefix + permissionCode);
    }
}
