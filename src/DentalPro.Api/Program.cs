using DentalPro.Api.Authentication;
using DentalPro.Api.Administration;
using DentalPro.Api.Authorization;
using DentalPro.Api.Identity;
using DentalPro.Api.Patients;
using DentalPro.Application.Patients;
using DentalPro.Infrastructure.DependencyInjection;
using DentalPro.Application.Administration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDentalProJwtBearerAuthentication();
builder.Services.AddPermissionAuthorization();
builder.Services.AddScoped<CreatePatient>();
builder.Services.AddScoped<ListPatients>();

var app = builder.Build();
if (args.Contains("--bootstrap-security", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ISecurityBootstrapper>().InitializeAsync();
    return;
}

app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapAdministrationEndpoints();
app.MapPatientEndpoints();
app.Run();

public partial class Program;
