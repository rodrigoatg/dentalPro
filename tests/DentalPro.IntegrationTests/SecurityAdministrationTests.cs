using DentalPro.Application.AccessControl;
using DentalPro.Application.Administration;
using DentalPro.Domain.AccessControl;
using DentalPro.Infrastructure.AccessControl;
using DentalPro.Infrastructure.Administration;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DentalPro.IntegrationTests;

public sealed class SecurityAdministrationTests
{
    private const string StrongPassword = "DentalPro-Test-Password-42!";

    [Fact]
    public async Task Bootstrap_IsIdempotentAndCreatesInitialMatrixAndAdministrator()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        await fixture.BootstrapAsync();

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DentalProDbContext>();
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        var permissions = await db.Permissions.AsNoTracking().ToListAsync();
        var assignments = await db.RolePermissions.AsNoTracking().ToListAsync();
        var administrator = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .GetUsersInRoleAsync(SystemRoles.Administrator);

        Assert.Equal(3, roles.Count);
        Assert.Equal(17, permissions.Count);
        Assert.Equal(31, assignments.Count);
        Assert.Single(administrator);
        Assert.True(administrator[0].IsActive);

        var roleIds = roles.ToDictionary(role => role.Name!, role => role.Id);
        var permissionIds = permissions.ToDictionary(permission => permission.Code, permission => permission.Id);
        foreach (var roleName in SystemRoles.All)
        {
            Assert.Contains(assignments, assignment => assignment.RoleId == roleIds[roleName] &&
                assignment.PermissionId == permissionIds[PermissionCatalog.PatientsView]);
            Assert.Contains(assignments, assignment => assignment.RoleId == roleIds[roleName] &&
                assignment.PermissionId == permissionIds[PermissionCatalog.PatientsCreate]);
        }

        var adminCodes = assignments.Where(assignment => assignment.RoleId == roleIds[SystemRoles.Administrator])
            .Select(assignment => permissions.Single(permission => permission.Id == assignment.PermissionId).Code)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(new PermissionCatalog().GetAll().Where(permission => permission.IsProtectedForAdministrator),
            permission => Assert.Contains(permission.Code, adminCodes));
        var protectedCodes = new PermissionCatalog().GetAll()
            .Where(permission => permission.IsProtectedForAdministrator)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(assignments.Where(assignment => assignment.RoleId != roleIds[SystemRoles.Administrator]),
            assignment => protectedCodes.Contains(
                permissions.Single(permission => permission.Id == assignment.PermissionId).Code));

        var appointmentMatrix = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [SystemRoles.Administrator] = [PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsViewAll,
                PermissionCatalog.AppointmentsCreate, PermissionCatalog.AppointmentsUpdate, PermissionCatalog.AppointmentsCancel,
                PermissionCatalog.AppointmentsConfirm, PermissionCatalog.AppointmentsComplete, PermissionCatalog.AppointmentsScheduleManage],
            [SystemRoles.Receptionist] = [PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsViewAll,
                PermissionCatalog.AppointmentsCreate, PermissionCatalog.AppointmentsUpdate, PermissionCatalog.AppointmentsCancel,
                PermissionCatalog.AppointmentsConfirm, PermissionCatalog.AppointmentsComplete],
            [SystemRoles.Dentist] = [PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsConfirm,
                PermissionCatalog.AppointmentsComplete]
        };
        foreach (var (roleName, codes) in appointmentMatrix)
        {
            var assignedCodes = assignments.Where(item => item.RoleId == roleIds[roleName])
                .Select(item => permissions.Single(permission => permission.Id == item.PermissionId).Code).ToHashSet();
            Assert.All(codes, code => Assert.Contains(code, assignedCodes));
            var forbidden = roleName switch
            {
                var name when name == SystemRoles.Administrator => Array.Empty<string>(),
                var name when name == SystemRoles.Receptionist => [PermissionCatalog.AppointmentsScheduleManage],
                _ => [PermissionCatalog.AppointmentsViewAll, PermissionCatalog.AppointmentsCreate,
                    PermissionCatalog.AppointmentsUpdate, PermissionCatalog.AppointmentsCancel,
                    PermissionCatalog.AppointmentsScheduleManage]
            };
            Assert.DoesNotContain(assignedCodes, forbidden.Contains);
        }
    }

    [Fact]
    public async Task Bootstrap_IsAdditiveForAppointmentAssignmentsAndPreservesAdministratorState()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        Guid administratorId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DentalProDbContext>();
            var dentist = await db.Roles.SingleAsync(role => role.Name == SystemRoles.Dentist);
            var create = await db.Permissions.SingleAsync(permission => permission.Code == PermissionCatalog.AppointmentsCreate);
            db.RolePermissions.Add(RolePermission.Create(dentist.Id, create.Id));
            var administrator = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .GetUsersInRoleAsync(SystemRoles.Administrator)).Single();
            administratorId = administrator.Id;
            administrator.IsActive = false;
            await db.SaveChangesAsync();
        }

        await fixture.BootstrapAsync();

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DentalProDbContext>();
        var updatedAdmin = await verifyDb.Users.SingleAsync(user => user.Id == administratorId);
        Assert.False(updatedAdmin.IsActive);
        var users = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.CheckPasswordAsync(updatedAdmin, StrongPassword));
        var dentistRoleId = await verifyDb.Roles.Where(role => role.Name == SystemRoles.Dentist)
            .Select(role => role.Id).SingleAsync();
        var createId = await verifyDb.Permissions.Where(permission => permission.Code == PermissionCatalog.AppointmentsCreate)
            .Select(permission => permission.Id).SingleAsync();
        Assert.Contains(await verifyDb.RolePermissions.AsNoTracking().ToListAsync(),
            assignment => assignment.RoleId == dentistRoleId && assignment.PermissionId == createId);
    }

    [Fact]
    public async Task UserAdministration_CreatesUsersChangesRolesAndRotatesSecurityStamp()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await users.CreateAsync(new CreateUserInput(
            "dentist.two", "dentist@example.test", "Ana", "Dentist", StrongPassword, [SystemRoles.Dentist]));
        var user = await userManager.FindByIdAsync(created.Id.ToString());
        Assert.NotNull(user);
        var oldStamp = await userManager.GetSecurityStampAsync(user);

        var changed = await users.SetRolesAsync(Guid.NewGuid(), user.Id, [SystemRoles.Receptionist]);
        user = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.NotNull(user);
        Assert.Equal([SystemRoles.Receptionist], changed.Roles);
        Assert.NotEqual(oldStamp, await userManager.GetSecurityStampAsync(user));
    }

    [Fact]
    public async Task RolePermissionAdministration_UsesCatalogAndChangesEffectivePermissionsImmediately()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DentalProDbContext>();
        var role = await db.Roles.SingleAsync(item => item.Name == SystemRoles.Dentist);
        var roleAdmin = scope.ServiceProvider.GetRequiredService<IRoleAdministrationService>();
        var effectiveQuery = scope.ServiceProvider.GetRequiredService<IEffectivePermissionQuery>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "dentist.permissions",
            FirstName = "Ana",
            LastName = "Dentist"
        };
        Assert.True((await userManager.CreateAsync(user, StrongPassword)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, SystemRoles.Dentist)).Succeeded);

        await roleAdmin.SetPermissionsAsync(role.Id, [PermissionCatalog.PatientsView]);
        var effective = await effectiveQuery.GetForUserAsync(user.Id);
        Assert.Contains(PermissionCatalog.PatientsView, effective);
        Assert.DoesNotContain(PermissionCatalog.PatientsCreate, effective);

        await Assert.ThrowsAsync<AdministrationValidationException>(() =>
            roleAdmin.SetPermissionsAsync(role.Id, ["future.unimplemented.permission"]));
    }

    [Fact]
    public async Task RolePermissionAdministration_CannotRemoveOrReassignProtectedAdministrativePermissions()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DentalProDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRoleAdministrationService>();
        var adminRole = await db.Roles.SingleAsync(role => role.Name == SystemRoles.Administrator);
        var dentistRole = await db.Roles.SingleAsync(role => role.Name == SystemRoles.Dentist);

        var updated = await service.SetPermissionsAsync(adminRole.Id, []);
        Assert.All(new PermissionCatalog().GetAll().Where(permission => permission.IsProtectedForAdministrator),
            permission => Assert.Contains(permission.Code, updated.AssignedCodes));
        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.SetPermissionsAsync(dentistRole.Id, [PermissionCatalog.UsersCreate]));
    }

    [Fact]
    public async Task UserAdministration_ProtectsTheLastAdministratorAndSelfAdministration()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DentalProDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
        var admin = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .GetUsersInRoleAsync(SystemRoles.Administrator)).Single();

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            users.SetRolesAsync(Guid.NewGuid(), admin.Id, []));
        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            users.SetRolesAsync(admin.Id, admin.Id, []));
        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            users.SetStatusAsync(Guid.NewGuid(), admin.Id, false));
        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            users.SetStatusAsync(admin.Id, admin.Id, false));

        Assert.True((await db.Users.SingleAsync(user => user.Id == admin.Id)).IsActive);
    }

    [Fact]
    public async Task UserAdministration_ConcurrentChangesCannotDeactivateTheLastActiveAdministrator()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await fixture.BootstrapAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            await users.CreateAsync(new CreateUserInput(
                "admin.two", null, "Second", "Admin", StrongPassword, [SystemRoles.Administrator]));
        }

        var administrators = await GetAdministratorIdsAsync(fixture.Services);
        var outcomes = await Task.WhenAll(administrators.Select(async id =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            try
            {
                await users.SetStatusAsync(Guid.NewGuid(), id, false);
                return true;
            }
            catch (AdministrationConflictException)
            {
                return false;
            }
        }));

        Assert.Single(outcomes, succeeded => succeeded);
        Assert.Single(outcomes, succeeded => !succeeded);
        Assert.Single(await GetActiveAdministratorIdsAsync(fixture.Services));
    }

    private static async Task<Guid[]> GetAdministratorIdsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await manager.GetUsersInRoleAsync(SystemRoles.Administrator)).Select(user => user.Id).ToArray();
    }

    private static async Task<Guid[]> GetActiveAdministratorIdsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DentalProDbContext>();
        var roleId = await db.Roles.Where(role => role.Name == SystemRoles.Administrator)
            .Select(role => role.Id).SingleAsync();
        return await db.UserRoles.Where(userRole => userRole.RoleId == roleId)
            .Join(db.Users.Where(user => user.IsActive), userRole => userRole.UserId, user => user.Id,
                (_, user) => user.Id)
            .ToArrayAsync();
    }

    private sealed class SecurityFixture : IAsyncDisposable
    {
        private SecurityFixture(ServiceProvider services) => Services = services;

        public ServiceProvider Services { get; }

        public static async Task<SecurityFixture> CreateAsync()
        {
            var name = "security-tests-" + Guid.NewGuid().ToString("N");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders());
            services.AddDbContext<DentalProDbContext>(options => options
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddSignInManager()
                .AddEntityFrameworkStores<DentalProDbContext>();
            services.AddSingleton<IPermissionCatalog, PermissionCatalog>();
            services.AddScoped<IEffectivePermissionQuery, EffectivePermissionQuery>();
            services.AddScoped<IUserAdministrationService, IdentityUserAdministrationService>();
            services.AddScoped<IRoleAdministrationService, IdentityRoleAdministrationService>();
            services.AddScoped<ISecurityBootstrapper, SecurityBootstrapper>();
            services.Configure<SecurityBootstrapOptions>(options =>
            {
                options.UserName = "first.admin";
                options.Email = "admin@example.test";
                options.FirstName = "First";
                options.LastName = "Administrator";
                options.Password = StrongPassword;
            });
            return new SecurityFixture(services.BuildServiceProvider(validateScopes: true));
        }

        public async Task BootstrapAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISecurityBootstrapper>().InitializeAsync();
        }

        public async ValueTask DisposeAsync() => await Services.DisposeAsync();
    }
}
