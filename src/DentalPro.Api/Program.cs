using DentalPro.Api.Patients;
using DentalPro.Application.Patients;
using DentalPro.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CreatePatient>();
builder.Services.AddScoped<ListPatients>();

var app = builder.Build();
app.MapPatientEndpoints();
app.Run();

public partial class Program;
