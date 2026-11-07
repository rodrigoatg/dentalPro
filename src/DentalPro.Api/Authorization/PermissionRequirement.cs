using Microsoft.AspNetCore.Authorization;

namespace DentalPro.Api.Authorization;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
