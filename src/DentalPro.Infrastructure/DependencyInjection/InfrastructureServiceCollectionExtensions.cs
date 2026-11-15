using DentalPro.Application.Patients;
using DentalPro.Application.Administration;
using DentalPro.Application.Identity;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Infrastructure.AccessControl;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Administration;
using DentalPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalPro.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DentalPro");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DentalPro using the ConnectionStrings__DentalPro environment variable.");
        }

        services.AddDbContext<DentalProDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<DentalProDbContext>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.IsValid(),
                "Jwt:Issuer, Jwt:Audience, a signing key of at least 32 bytes, and a token lifetime from 1 to 1440 minutes are required.")
            .ValidateOnStart();
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IUserAuthenticationService, IdentityUserAuthenticationService>();
        services.AddScoped<IUserSessionValidator, IdentityUserSessionValidator>();
        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();
        services.AddScoped<IEffectivePermissionQuery, EffectivePermissionQuery>();
        services.AddScoped<IUserAdministrationService, IdentityUserAdministrationService>();
        services.AddScoped<IRoleAdministrationService, IdentityRoleAdministrationService>();
        services.AddScoped<ISecurityBootstrapper, SecurityBootstrapper>();
        services.AddOptions<SecurityBootstrapOptions>()
            .Bind(configuration.GetSection(SecurityBootstrapOptions.SectionName));
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAppointmentParticipantQuery, AppointmentParticipantQuery>();
        services.AddScoped<IAppointmentUnitOfWork, AppointmentUnitOfWork>();
        services.AddScoped<IAuditEventWriter, AuditEventWriter>();
        services.AddScoped<ClinicSchedulePersistence>();
        services.AddScoped<IClinicScheduleQuery>(provider => provider.GetRequiredService<ClinicSchedulePersistence>());
        services.AddScoped<IClinicScheduleRepository>(provider => provider.GetRequiredService<ClinicSchedulePersistence>());

        return services;
    }
}
