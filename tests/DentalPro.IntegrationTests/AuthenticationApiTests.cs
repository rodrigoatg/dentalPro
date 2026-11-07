using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using DentalPro.Api.Authentication;
using DentalPro.Api.Administration;
using DentalPro.Api.Authorization;
using DentalPro.Api.Identity;
using DentalPro.Api.Patients;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Patients;
using DentalPro.Application.Identity;
using DentalPro.Infrastructure.DependencyInjection;
using DentalPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DentalPro.IntegrationTests;

[Collection("Patient API")]
public sealed class AuthenticationApiTests
{
    private const string TestSigningKey = "test-signing-key-with-at-least-32-bytes-long";
    private const string TestIssuer = "DentalPro.Tests";
    private const string TestAudience = "DentalPro.Api.Tests";
    private const string TestPassword = "Sufficient-Test-Password-123!";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSignedTokenAndIdentityClaims()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("dentist.one", TestPassword, roles: ["Dentist"]);

        var response = await api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.UserName, TestPassword));
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.UserId);
        Assert.Equal(user.UserName, body.UserName);
        Assert.Equal(["Dentist"], body.Roles);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Contains(jwt.Claims, claim => claim.Type == "role" && claim.Value == "Dentist");
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type.Contains("permission", StringComparison.OrdinalIgnoreCase));

        using var client = api.CreateClientWithBearer(body.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/__tests/authenticated")).StatusCode);
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ReturnsUnauthorized()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("reception.one", TestPassword);

        var response = await api.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(user.UserName, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsSameUnauthorizedResponseAsBadPassword()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();

        var response = await api.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("missing.user", TestPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Invalid username or password", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithCorrectPasswordForDisabledUser_ReturnsForbidden()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("disabled.user", TestPassword, isActive: false);

        var response = await api.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(user.UserName, TestPassword));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_RejectsMalformedOrTamperedJwt()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("jwt.user", TestPassword);
        var token = await api.LoginAsync(user.UserName!, TestPassword);
        var segments = token.Split('.');
        segments[2] = (segments[2][0] == 'A' ? 'B' : 'A') + segments[2][1..];
        var tamperedToken = string.Join('.', segments);

        using var client = api.CreateClientWithBearer(tamperedToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/__tests/authenticated")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_RejectsExpiredJwt()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("expired.user", TestPassword);
        var token = api.CreateExpiredToken(user);

        using var client = api.CreateClientWithBearer(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/__tests/authenticated")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_RejectsJwtAfterSecurityStampChanges()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("stamp.user", TestPassword);
        var token = await api.LoginAsync(user.UserName!, TestPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        using var client = api.CreateClientWithBearer(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/__tests/authenticated")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_RejectsJwtWhenUserNoLongerExists()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("deleted.user", TestPassword);
        var token = await api.LoginAsync(user.UserName!, TestPassword);
        api.DeleteUser(user.Id);

        using var client = api.CreateClientWithBearer(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/__tests/authenticated")).StatusCode);
    }

    [Fact]
    public async Task PatientEndpoints_AllowAnonymousLogin_AndRequireAuthentication()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("anonymous.login", TestPassword);

        Assert.Equal(HttpStatusCode.OK,
            (await api.Client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(user.UserName!, TestPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/api/patients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await api.Client.PostAsJsonAsync("/api/patients", new { })).StatusCode);
    }

    [Fact]
    public async Task PermissionAuthorization_CombinesPermissionsFromMultipleCurrentRoles()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("multi.role", TestPassword, roles: ["Dentist", "Receptionist"]);
        api.GrantRolePermission("Dentist", PermissionCatalog.PatientsView);
        api.GrantRolePermission("Receptionist", PermissionCatalog.PatientsCreate);
        var token = await api.LoginAsync(user.UserName!, TestPassword);
        using var client = api.CreateClientWithBearer(token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/__tests/permissions/patients.view")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/__tests/permissions/patients.create")).StatusCode);
    }

    [Fact]
    public async Task PermissionAuthorization_DeniesMissingPermissionAndUserWithoutRoles()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var receptionist = api.AddUser("reception.only", TestPassword, roles: ["Receptionist"]);
        var noRoles = api.AddUser("no.roles", TestPassword);
        api.GrantRolePermission("Dentist", PermissionCatalog.PatientsView);

        using var receptionistClient = api.CreateClientWithBearer(
            await api.LoginAsync(receptionist.UserName!, TestPassword));
        using var noRolesClient = api.CreateClientWithBearer(await api.LoginAsync(noRoles.UserName!, TestPassword));

        Assert.Equal(HttpStatusCode.Forbidden,
            (await receptionistClient.GetAsync("/__tests/permissions/patients.view")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await noRolesClient.GetAsync("/__tests/permissions/patients.view")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionistClient.GetAsync("/api/patients")).StatusCode);
    }

    [Fact]
    public async Task PermissionAuthorization_ReflectsAssignmentChangesOnTheNextRequest()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        var user = api.AddUser("live.permissions", TestPassword, roles: ["Dentist"]);
        var token = await api.LoginAsync(user.UserName!, TestPassword);
        using var client = api.CreateClientWithBearer(token);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/__tests/permissions/patients.view")).StatusCode);
        api.GrantRolePermission("Dentist", PermissionCatalog.PatientsView);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/__tests/permissions/patients.view")).StatusCode);
        api.RevokeRolePermission("Dentist", PermissionCatalog.PatientsView);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/__tests/permissions/patients.view")).StatusCode);
    }

    [Fact]
    public async Task AdministrationEndpoints_RequireTheirCatalogPermissions_WhileMeRequiresOnlyAuthentication()
    {
        await using var api = await AuthenticationTestApi.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/api/admin/users")).StatusCode);
        var user = api.AddUser("reception.admin-api", TestPassword, roles: ["Receptionist"]);
        using var client = api.CreateClientWithBearer(await api.LoginAsync(user.UserName!, TestPassword));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var protectedResponses = new[]
        {
            await client.GetAsync("/api/admin/users"),
            await client.PostAsJsonAsync("/api/admin/users", new { }),
            await client.PutAsJsonAsync($"/api/admin/users/{userId}/roles", new { roles = Array.Empty<string>() }),
            await client.PatchAsJsonAsync($"/api/admin/users/{userId}/status", new { isActive = false }),
            await client.GetAsync("/api/admin/roles"),
            await client.GetAsync($"/api/admin/roles/{roleId}/permissions"),
            await client.PutAsJsonAsync($"/api/admin/roles/{roleId}/permissions", new { permissionCodes = Array.Empty<string>() }),
            await client.GetAsync("/api/admin/permissions")
        };

        Assert.All(protectedResponses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    private sealed class AuthenticationTestApi : IAsyncDisposable
    {
        private readonly WebApplication _application;
        private readonly InMemoryIdentityUserStore _userStore;
        private readonly MutableEffectivePermissionQuery _effectivePermissions;

        private AuthenticationTestApi(
            WebApplication application,
            InMemoryIdentityUserStore userStore,
            MutableEffectivePermissionQuery effectivePermissions)
        {
            _application = application;
            _userStore = userStore;
            _effectivePermissions = effectivePermissions;
            Client = application.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<AuthenticationTestApi> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DentalPro"] = "Host=localhost;Database=unused",
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenLifetimeMinutes"] = "15"
            });

            builder.Services.AddInfrastructure(builder.Configuration);
            var userStore = new InMemoryIdentityUserStore();
            var effectivePermissions = new MutableEffectivePermissionQuery(userStore);
            builder.Services.RemoveAll<IUserStore<ApplicationUser>>();
            builder.Services.AddSingleton(userStore);
            builder.Services.AddSingleton<IUserStore<ApplicationUser>>(services =>
                services.GetRequiredService<InMemoryIdentityUserStore>());
            builder.Services.AddDentalProJwtBearerAuthentication();
            builder.Services.Replace(ServiceDescriptor.Singleton<IEffectivePermissionQuery>(effectivePermissions));
            builder.Services.AddPermissionAuthorization();
            builder.Services.AddScoped<CreatePatient>();
            builder.Services.AddScoped<ListPatients>();

            var application = builder.Build();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapAuthEndpoints();
            application.MapAdministrationEndpoints();
            application.MapPatientEndpoints();
            application.MapGet("/__tests/authenticated", (ClaimsPrincipal principal) =>
                principal.Identity?.IsAuthenticated == true ? Results.Ok() : Results.Unauthorized())
                .RequireAuthorization();
            application.MapGet("/__tests/permissions/patients.view", () => Results.Ok())
                .RequirePermission(PermissionCatalog.PatientsView);
            application.MapGet("/__tests/permissions/patients.create", () => Results.Ok())
                .RequirePermission(PermissionCatalog.PatientsCreate);
            await application.StartAsync();

            return new AuthenticationTestApi(application, userStore, effectivePermissions);
        }

        public ApplicationUser AddUser(
            string userName,
            string password,
            bool isActive = true,
            IReadOnlyList<string>? roles = null)
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                IsActive = isActive
            };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
            _userStore.Add(user, roles ?? []);
            return user;
        }

        public async Task<string> LoginAsync(string userName, string password)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return body?.AccessToken ?? throw new InvalidOperationException("Login did not return an access token.");
        }

        public string CreateExpiredToken(ApplicationUser user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new Claim(JwtTokenGenerator.SecurityStampFingerprintClaim,
                    JwtTokenGenerator.CreateSecurityStampFingerprint(user.SecurityStamp!))
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var jwt = new JwtSecurityToken(
                TestIssuer,
                TestAudience,
                claims,
                now.AddMinutes(-20),
                now.AddMinutes(-10),
                credentials);

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }

        public HttpClient CreateClientWithBearer(string token)
        {
            var client = _application.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public void DeleteUser(Guid userId) => _userStore.Delete(userId);

        public void GrantRolePermission(string role, string permission) =>
            _effectivePermissions.Grant(role, permission);

        public void RevokeRolePermission(string role, string permission) =>
            _effectivePermissions.Revoke(role, permission);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }

    private sealed class MutableEffectivePermissionQuery(InMemoryIdentityUserStore userStore)
        : IEffectivePermissionQuery
    {
        private readonly HashSet<(string Role, string Permission)> _assignments = [];

        public void Grant(string role, string permission) => _assignments.Add((role, permission));

        public void Revoke(string role, string permission) => _assignments.Remove((role, permission));

        public Task<IReadOnlySet<string>> GetForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var result = userStore.GetRoles(userId)
                .SelectMany(role => _assignments
                    .Where(assignment => assignment.Role == role)
                    .Select(assignment => assignment.Permission))
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult<IReadOnlySet<string>>(result);
        }

        public async Task<bool> HasPermissionAsync(
            Guid userId,
            string permissionCode,
            CancellationToken cancellationToken = default) =>
            (await GetForUserAsync(userId, cancellationToken)).Contains(permissionCode);
    }

    private sealed class InMemoryIdentityUserStore :
        IUserPasswordStore<ApplicationUser>,
        IUserRoleStore<ApplicationUser>,
        IUserLockoutStore<ApplicationUser>,
        IUserSecurityStampStore<ApplicationUser>
    {
        private readonly Dictionary<Guid, ApplicationUser> _users = [];
        private readonly Dictionary<Guid, HashSet<string>> _roles = [];

        public void Add(ApplicationUser user, IEnumerable<string> roles)
        {
            _users.Add(user.Id, user);
            _roles[user.Id] = new HashSet<string>(roles, StringComparer.Ordinal);
        }

        public void Delete(Guid userId)
        {
            _users.Remove(userId);
            _roles.Remove(userId);
        }

        public IReadOnlyCollection<string> GetRoles(Guid userId) =>
            _roles.TryGetValue(userId, out var roles) ? roles : [];

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users[user.Id] = user;
            _roles.TryAdd(user.Id, new HashSet<string>(StringComparer.Ordinal));
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delete(user.Id);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Guid.TryParse(userId, out var id) && _users.TryGetValue(id, out var user)
                ? user
                : null);
        }

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_users.Values.SingleOrDefault(user =>
                string.Equals(user.NormalizedUserName, normalizedUserName, StringComparison.Ordinal)));
        }

        public string GetUserId(ApplicationUser user) => user.Id.ToString();

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.Id.ToString());
        }

        public string? GetUserName(ApplicationUser user) => user.UserName;

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        }

        public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _roles[user.Id].Add(roleName);
            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _roles[user.Id].Remove(roleName);
            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IList<string> result = _roles[user.Id].ToList();
            return Task.FromResult(result);
        }

        public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_roles[user.Id].Contains(roleName));
        }

        public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IList<ApplicationUser> result = _users.Values
                .Where(user => _roles[user.Id].Contains(roleName))
                .ToList();
            return Task.FromResult(result);
        }

        public Task SetLockoutEndDateAsync(
            ApplicationUser user,
            DateTimeOffset? lockoutEnd,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.LockoutEnd);
        }

        public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(++user.AccessFailedCount);
        }

        public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.LockoutEnabled);
        }

        public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.LockoutEnabled = enabled;
            return Task.CompletedTask;
        }

        public Task SetSecurityStampAsync(ApplicationUser user, string? stamp, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.SecurityStamp);
        }

        public void Dispose()
        {
        }
    }
}
