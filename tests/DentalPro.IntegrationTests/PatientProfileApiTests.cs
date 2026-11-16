using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using DentalPro.Api;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Identity;
using DentalPro.Application.Patients;
using DentalPro.Api.Patients;
using DentalPro.Domain.Patients;
using DentalPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DentalPro.IntegrationTests;

[Collection("Patient API")]
public sealed class PatientProfileApiTests
{
    [Fact]
    public async Task PatientProfile_CanBeReadUpdatedAndSearchedWithoutLosingMedicalRecord()
    {
        await using var factory = new PatientFactory([PermissionCatalog.PatientsView, PermissionCatalog.PatientsCreate, PermissionCatalog.PatientsUpdate]);
        using var client = factory.Client();
        var create = await client.PostAsJsonAsync("/api/patients", new
        {
            documentType = "DNI", documentNumber = "100", firstName = "Ana", lastName = "Paz"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PatientResponse>();
        Assert.NotNull(created);

        var listed = await client.GetFromJsonAsync<PatientListItemResponse[]>("/api/patients");
        var listedPatient = Assert.Single(listed!);
        Assert.Equal(created.Id, listedPatient.Id);
        var read = await client.GetAsync($"/api/patients/{listedPatient.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var profile = await read.Content.ReadFromJsonAsync<PatientResponse>();
        Assert.Equal(created.MedicalRecordNumber, profile!.MedicalRecordNumber);

        var updated = await client.PutAsJsonAsync($"/api/patients/{created.Id}", new
        {
            documentType = "DNI", documentNumber = "100", firstName = "Ana María", lastName = "Paz",
            dateOfBirth = (DateOnly?)null, phone = "555", email = "ana@example.com", address = "Calle 1"
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        var after = await (await client.GetAsync($"/api/patients/{created.Id}")).Content.ReadFromJsonAsync<PatientResponse>();
        Assert.Equal("Ana María", after!.FirstName);
        Assert.Equal(created.MedicalRecordNumber, after.MedicalRecordNumber);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/patients?search=100")).StatusCode);
    }

    [Fact]
    public async Task PatientSearch_BindsAndMatchesFullPartialCaseInsensitiveNameSurnameAndDocument_AndClearingRestoresAll()
    {
        await using var factory = new PatientFactory([PermissionCatalog.PatientsView, PermissionCatalog.PatientsCreate]);
        using var client = factory.Client();
        var first = await client.PostAsJsonAsync("/api/patients", new
        {
            documentType = "DNI", documentNumber = "AR-90871", firstName = "María", lastName = "Fernández"
        });
        var second = await client.PostAsJsonAsync("/api/patients", new
        {
            documentType = "DNI", documentNumber = "AR-11442", firstName = "Sofía", lastName = "Ramos"
        });
        var firstPatient = await first.Content.ReadFromJsonAsync<PatientResponse>();
        var secondPatient = await second.Content.ReadFromJsonAsync<PatientResponse>();
        Assert.NotNull(firstPatient);
        Assert.NotNull(secondPatient);

        async Task<PatientListItemResponse[]> Search(string? term)
        {
            var uri = term is null ? "/api/patients" : $"/api/patients?search={Uri.EscapeDataString(term)}";
            return (await client.GetFromJsonAsync<PatientListItemResponse[]>(uri))!;
        }

        Assert.Contains(await Search("María Fernández"), p => p.Id == firstPatient.Id);
        Assert.Contains(await Search("  mAr   fErN "), p => p.Id == firstPatient.Id);
        Assert.Contains(await Search("rNá"), p => p.Id == firstPatient.Id);
        Assert.Contains(await Search("908"), p => p.Id == firstPatient.Id);
        Assert.DoesNotContain(await Search("908"), p => p.Id == secondPatient.Id);
        Assert.Equal(2, (await Search("")).Length);
        Assert.Equal(2, (await Search("   ")).Length);
        Assert.Equal(2, (await Search(null)).Length);
        Assert.Equal(2, (await client.GetFromJsonAsync<PatientCountResponse>("/api/patients/count"))!.Count);
    }

    [Fact]
    public async Task PatientUpdate_RequiresItsPermissionAndReturnsNotFoundForUnknownPatient()
    {
        await using var factory = new PatientFactory([PermissionCatalog.PatientsView]);
        using var client = factory.Client();
        var denied = await client.PutAsJsonAsync($"/api/patients/{Guid.NewGuid()}", new
        {
            documentType = "DNI", documentNumber = "100", firstName = "A", lastName = "B"
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/patients/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task PatientUpdate_MapsDuplicateDocumentAndInvalidInput()
    {
        await using var factory = new PatientFactory([PermissionCatalog.PatientsView, PermissionCatalog.PatientsCreate, PermissionCatalog.PatientsUpdate]);
        using var client = factory.Client();
        var first = await client.PostAsJsonAsync("/api/patients", new { documentType = "DNI", documentNumber = "100", firstName = "A", lastName = "B" });
        var second = await client.PostAsJsonAsync("/api/patients", new { documentType = "DNI", documentNumber = "200", firstName = "C", lastName = "D" });
        var a = await first.Content.ReadFromJsonAsync<PatientResponse>();
        var b = await second.Content.ReadFromJsonAsync<PatientResponse>();
        var duplicate = await client.PutAsJsonAsync($"/api/patients/{b!.Id}", new { documentType = "DNI", documentNumber = "100", firstName = "C", lastName = "D" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var invalid = await client.PutAsJsonAsync($"/api/patients/{a!.Id}", new { documentType = "DNI", documentNumber = "100", firstName = " ", lastName = "D", email = "bad" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private sealed class PatientFactory(string[] permissions) : WebApplicationFactory<global::Program>
    {
        private const string Key = "patient-profile-api-test-signing-key-long-enough";
        private readonly InMemoryPatients _patients = new();
        private readonly string? _oldConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DentalPro");
        private readonly string? _oldSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");

        public HttpClient Client()
        {
            var client = CreateClient();
            var jwt = Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
            var user = new ApplicationUser { UserName = "patient-profile-test" };
            var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtTokenGenerator.SecurityStampFingerprintClaim, JwtTokenGenerator.CreateSecurityStampFingerprint(user.SecurityStamp!)) };
            var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(jwt.AccessTokenLifetimeMinutes), new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", "Host=localhost;Database=unused");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", Key);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPatientRepository>(); services.AddSingleton<IPatientRepository>(_patients);
                services.RemoveAll<IEffectivePermissionQuery>(); services.AddSingleton<IEffectivePermissionQuery>(new TestPermissions(permissions));
                services.RemoveAll<IUserSessionValidator>(); services.AddSingleton<IUserSessionValidator, ActiveSession>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", _oldConnectionString);
                Environment.SetEnvironmentVariable("Jwt__SigningKey", _oldSigningKey);
            }
        }
    }

    private sealed class TestPermissions(string[] permissions) : IEffectivePermissionQuery
    {
        public Task<IReadOnlySet<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(permissions.ToHashSet(StringComparer.Ordinal));
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) => Task.FromResult(permissions.Contains(permissionCode));
    }
    private sealed class ActiveSession : IUserSessionValidator
    {
        public Task<bool> IsCurrentAsync(Guid userId, string securityStampFingerprint, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class InMemoryPatients : IPatientRepository
    {
        private readonly List<Patient> _items = [];
        private long _number = 1;
        public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
        {
            if (_items.Any(item => item.DocumentType == patient.DocumentType && item.DocumentNumber == patient.DocumentNumber)) throw new PatientDocumentAlreadyExistsException();
            patient.MedicalRecord.AssignMedicalRecordNumber(_number++); _items.Add(patient); return Task.CompletedTask;
        }
        public Task<IReadOnlyList<PatientListItem>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientListItem>>(_items.Select(Map).ToArray());
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.Count);
        public Task<IReadOnlyList<PatientListItem>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        {
            var terms = search?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            return Task.FromResult<IReadOnlyList<PatientListItem>>(_items.Where(item => terms.All(term =>
                new[] { item.FirstName, item.LastName, item.DocumentNumber, item.DocumentType }
                    .Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase)))).Select(Map).ToArray());
        }
        public Task<PatientDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = _items.SingleOrDefault(x => x.Id == id);
            return Task.FromResult(item is null ? null : new PatientDetails(item.Id, item.DocumentType, item.DocumentNumber, item.FirstName,
                item.LastName, item.DateOfBirth, item.Phone, item.Email, item.Address, item.IsActive, item.CreatedAt, item.UpdatedAt, item.MedicalRecord.MedicalRecordNumber!.Value));
        }
        public Task UpdateAsync(Guid id, UpdatePatientData data, CancellationToken cancellationToken = default)
        {
            var item = _items.SingleOrDefault(x => x.Id == id) ?? throw new PatientNotFoundException();
            if (_items.Any(x => x.Id != id && x.DocumentType == data.DocumentType && x.DocumentNumber == data.DocumentNumber)) throw new PatientDocumentAlreadyExistsException();
            item.UpdatePersonalInformation(data.DocumentType, data.DocumentNumber, data.FirstName, data.LastName, data.DateOfBirth, data.Phone, data.Email, data.Address);
            return Task.CompletedTask;
        }
        private static PatientListItem Map(Patient p) => new(p.Id, p.DocumentType, p.DocumentNumber, p.FirstName, p.LastName,
            p.DateOfBirth, p.Phone, p.Email, p.IsActive, p.CreatedAt, p.MedicalRecord.MedicalRecordNumber!.Value);
    }
}
