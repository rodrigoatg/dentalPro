using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalPro.Application.Patients;
using DentalPro.Domain.Patients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DentalPro.IntegrationTests;

[Collection("Patient API")]
public sealed class PatientApiTests
{
    [Fact]
    public async Task PostPatient_CreatesPatientAndReturnsMedicalRecordNumber_AndGetListsIt()
    {
        await using var factory = new PatientApiFactory();
        using var client = factory.CreateClient();
        var request = new
        {
            documentType = "DNI",
            documentNumber = "12345678",
            firstName = "Ana",
            lastName = "Pérez",
            dateOfBirth = (DateOnly?)null,
            phone = (string?)null,
            email = (string?)null,
            address = (string?)null
        };

        var createResponse = await client.PostAsJsonAsync("/api/patients", request);
        var body = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("medicalRecordNumber").GetInt64());

        var listResponse = await client.GetAsync("/api/patients");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var patients = await listResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(patients);
        Assert.Single(patients);
        Assert.Equal("Ana", patients[0].GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task PostPatient_WhenDocumentAlreadyExists_ReturnsConflictWithoutExceptionDetails()
    {
        await using var factory = new PatientApiFactory();
        using var client = factory.CreateClient();
        var request = new
        {
            documentType = "DNI",
            documentNumber = "12345678",
            firstName = "Ana",
            lastName = "Pérez"
        };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/patients", request)).StatusCode);
        var duplicateResponse = await client.PostAsJsonAsync("/api/patients", request);
        var responseBody = await duplicateResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.DoesNotContain("PostgresException", responseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostPatient_WhenDomainValidationFails_ReturnsBadRequest()
    {
        await using var factory = new PatientApiFactory();
        using var client = factory.CreateClient();
        var request = new
        {
            documentType = "DNI",
            documentNumber = "12345678",
            firstName = " ",
            lastName = "Pérez"
        };

        var response = await client.PostAsJsonAsync("/api/patients", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class PatientApiFactory : WebApplicationFactory<Program>
    {
        private readonly string? _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DentalPro");

        public PatientApiFactory() =>
            Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", "Host=localhost;Database=unused");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", _previousConnectionString);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPatientRepository>();
                services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
            });
        }
    }

    private sealed class InMemoryPatientRepository : IPatientRepository
    {
        private readonly List<Patient> _patients = [];
        private long _nextMedicalRecordNumber = 1;

        public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
        {
            if (_patients.Any(existing =>
                    existing.DocumentType == patient.DocumentType && existing.DocumentNumber == patient.DocumentNumber))
            {
                throw new PatientDocumentAlreadyExistsException();
            }

            patient.MedicalRecord.AssignMedicalRecordNumber(_nextMedicalRecordNumber++);
            _patients.Add(patient);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PatientListItem>> ListAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PatientListItem> items = _patients.Select(patient => new PatientListItem(
                patient.Id,
                patient.DocumentType,
                patient.DocumentNumber,
                patient.FirstName,
                patient.LastName,
                patient.DateOfBirth,
                patient.Phone,
                patient.Email,
                patient.IsActive,
                patient.CreatedAt,
                patient.MedicalRecord.MedicalRecordNumber!.Value)).ToArray();
            return Task.FromResult(items);
        }
    }
}


