using DentalPro.Application.Patients;
using DentalPro.Infrastructure.Persistence;
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
        services.AddScoped<IPatientRepository, PatientRepository>();

        return services;
    }
}
