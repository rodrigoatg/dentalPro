using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DentalPro.Application.Patients;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Identity;
using DentalPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using DentalPro.Domain.Patients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DentalPro.IntegrationTests;

[Collection("Patient API")]
public sealed class PatientApiTests
{
    [Fact]
    public async Task PostPatient_CreatesPatientAndReturnsMedicalRecordNumber_AndGetListsIt()
    {
        await using var factory = new PatientApiFactory();
        using var client = factory.CreateAuthenticatedClient();
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
        using var client = factory.CreateAuthenticatedClient();
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
        using var client = factory.CreateAuthenticatedClient();
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
        private const string SigningKey = "patient-api-test-signing-key-at-least-32-bytes";
        private readonly string? _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DentalPro");
        private readonly string? _previousJwtSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");

        public PatientApiFactory()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", "Host=localhost;Database=unused");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        }

        public HttpClient CreateAuthenticatedClient()
        {
            var client = CreateClient();
            var jwtOptions = Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
            var user = new ApplicationUser { UserName = "patient-test" };
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new Claim(JwtTokenGenerator.SecurityStampFingerprintClaim,
                    JwtTokenGenerator.CreateSecurityStampFingerprint(user.SecurityStamp!))
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                jwtOptions.Issuer,
                jwtOptions.Audience,
                claims,
                now,
                now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes),
                credentials);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", _previousConnectionString);
                Environment.SetEnvironmentVariable("Jwt__SigningKey", _previousJwtSigningKey);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPatientRepository>();
                services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
                services.RemoveAll<IEffectivePermissionQuery>();
                services.AddSingleton<IEffectivePermissionQuery, PatientPermissionQuery>();
                services.RemoveAll<IUserSessionValidator>();
                services.AddSingleton<IUserSessionValidator, ActiveTestSessionValidator>();
            });
        }
    }

    private sealed class PatientPermissionQuery : IEffectivePermissionQuery
    {
        public Task<IReadOnlySet<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(
                [PermissionCatalog.PatientsView, PermissionCatalog.PatientsCreate], StringComparer.Ordinal));

        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissionCode is PermissionCatalog.PatientsView or PermissionCatalog.PatientsCreate);
    }

    private sealed class ActiveTestSessionValidator : IUserSessionValidator
    {
        public Task<bool> IsCurrentAsync(Guid userId, string securityStampFingerprint, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty && !string.IsNullOrWhiteSpace(securityStampFingerprint));
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


