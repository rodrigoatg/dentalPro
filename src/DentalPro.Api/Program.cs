using DentalPro.Api.Authentication;
using DentalPro.Api.Administration;
using DentalPro.Api.Authorization;
using DentalPro.Api.Identity;
using DentalPro.Api.Patients;
using DentalPro.Api.Appointments;
using DentalPro.Application.Patients;
using DentalPro.Application.Appointments;
using DentalPro.Infrastructure.DependencyInjection;
using DentalPro.Application.Administration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDentalProJwtBearerAuthentication();
builder.Services.AddPermissionAuthorization();
builder.Services.AddScoped<CreatePatient>();
builder.Services.AddScoped<ListPatients>();
builder.Services.AddScoped<ListAppointments>();
builder.Services.AddScoped<GetAppointmentAvailability>();
builder.Services.AddScoped<CreateAppointment>();
builder.Services.AddScoped<RescheduleAppointment>();
builder.Services.AddScoped<CancelAppointment>();
builder.Services.AddScoped<ConfirmAppointment>();
builder.Services.AddScoped<CompleteAppointment>();
builder.Services.AddScoped<GetClinicSchedule>();
builder.Services.AddScoped<CreateClinicSchedule>();
builder.Services.AddScoped<UpdateClinicSchedule>();
builder.Services.AddScoped<SetClinicScheduleException>();
builder.Services.AddScoped<RemoveClinicScheduleException>();

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
app.MapAppointmentEndpoints();
app.MapClinicScheduleEndpoints();
app.Run();

public partial class Program;
