using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Application.Identity;
using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DentalPro.IntegrationTests;

[Collection("Patient API")]
public sealed class AppointmentApiTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Endpoints_RequireAuthenticationAndTheirSpecificPermission()
    {
        await using var factory = new AppointmentApiFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/appointments?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z")).StatusCode);

        var actorId = Guid.NewGuid();
        using var authenticated = factory.CreateAuthenticatedClient(actorId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await authenticated.GetAsync("/api/appointments?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await authenticated.GetAsync("/api/appointments/dentists")).StatusCode);
    }

    [Fact]
    public async Task ClinicalRecordRoutes_RequireAuthenticationAndExplicitClinicalPermissions()
    {
        await using var factory = new AppointmentApiFactory();
        var path = $"/api/patients/{Guid.NewGuid()}/medical-record";
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync($"{path}/entries", new { })).StatusCode);

        using var patientViewer = factory.CreateAuthenticatedClient(Guid.NewGuid(), PermissionCatalog.PatientsView);
        Assert.Equal(HttpStatusCode.Forbidden, (await patientViewer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await patientViewer.PostAsJsonAsync($"{path}/entries", new { })).StatusCode);
    }

    [Fact]
    public async Task ClinicScheduleRoutes_ExposeSettingsToAgendaViewAndProtectAdministration()
    {
        await using var factory = new AppointmentApiFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/appointments/schedule/settings")).StatusCode);

        using var viewer = factory.CreateAuthenticatedClient(Guid.NewGuid(), PermissionCatalog.AppointmentsView);
        Assert.Equal(HttpStatusCode.OK,
            (await viewer.GetAsync("/api/appointments/schedule/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.GetAsync("/api/appointments/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PostAsJsonAsync("/api/appointments/schedule", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PutAsJsonAsync("/api/appointments/schedule", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PutAsJsonAsync("/api/appointments/schedule/exceptions/2030-01-01", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.DeleteAsync("/api/appointments/schedule/exceptions/2030-01-01")).StatusCode);
    }

    [Fact]
    public async Task MutationEndpoints_RequireTheirOwnPermission()
    {
        await using var factory = new AppointmentApiFactory();
        var allActions = new[]
        {
            PermissionCatalog.AppointmentsCreate,
            PermissionCatalog.AppointmentsUpdate,
            PermissionCatalog.AppointmentsCancel,
            PermissionCatalog.AppointmentsConfirm,
            PermissionCatalog.AppointmentsComplete
        };
        var requests = new Dictionary<string, Func<HttpClient, Task<HttpResponseMessage>>>
        {
            [PermissionCatalog.AppointmentsCreate] = client => client.PostAsJsonAsync("/api/appointments", new
            {
                patientId = Guid.NewGuid(), dentistId = Guid.NewGuid(), startDateTime = Start
            }),
            [PermissionCatalog.AppointmentsUpdate] = client => client.PostAsJsonAsync(
                $"/api/appointments/{Guid.NewGuid()}/reschedule", new { dentistId = Guid.NewGuid(), startDateTime = Start }),
            [PermissionCatalog.AppointmentsCancel] = client => client.PostAsync(
                $"/api/appointments/{Guid.NewGuid()}/cancel", null),
            [PermissionCatalog.AppointmentsConfirm] = client => client.PostAsync(
                $"/api/appointments/{Guid.NewGuid()}/confirm", null),
            [PermissionCatalog.AppointmentsComplete] = client => client.PostAsync(
                $"/api/appointments/{Guid.NewGuid()}/complete", null)
        };

        foreach (var (requiredPermission, send) in requests)
        {
            var granted = allActions.Where(permission => permission != requiredPermission)
                .Append(PermissionCatalog.AppointmentsView).ToArray();
            using var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), granted);
            using var response = await send(client);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ListAppointments_UsesActorSubToRestrictDentistScopeAndMapsNames()
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        var otherDentistId = Guid.NewGuid();
        factory.Repository.Seed(Appointment.Create(Guid.NewGuid(), actorId, Start, 30, "own"));
        factory.Repository.Seed(Appointment.Create(Guid.NewGuid(), otherDentistId, Start.AddHours(1), 30, "other"));
        using var client = factory.CreateAuthenticatedClient(actorId, PermissionCatalog.AppointmentsView);

        var response = await client.GetAsync("/api/appointments?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(rows);
        var own = Assert.Single(rows);
        Assert.Equal(actorId, own.GetProperty("dentistId").GetGuid());
        Assert.Equal("Patient", own.GetProperty("patientFirstName").GetString());
        Assert.Equal("Dentist", own.GetProperty("dentistFirstName").GetString());
        Assert.Equal(30, own.GetProperty("durationMinutes").GetInt32());

        var narrowed = await client.GetAsync($"/api/appointments?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z&dentistId={otherDentistId}");
        Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
        Assert.Empty(await narrowed.Content.ReadFromJsonAsync<JsonElement[]>() ?? []);
    }

    [Fact]
    public async Task CreateAndMutations_UseSpecificPermissionsAndActorFromJwt()
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        var suppliedActorId = Guid.NewGuid();
        using var client = factory.CreateAuthenticatedClient(actorId,
            PermissionCatalog.AppointmentsView,
            PermissionCatalog.AppointmentsCreate,
            PermissionCatalog.AppointmentsUpdate,
            PermissionCatalog.AppointmentsCancel,
            PermissionCatalog.AppointmentsConfirm,
            PermissionCatalog.AppointmentsComplete);

        var createdResponse = await client.PostAsJsonAsync("/api/appointments", new
        {
            patientId = Guid.NewGuid(),
            dentistId = actorId,
            startDateTime = Start,
            durationMinutes = (int?)null,
            notes = "test",
            actorId = suppliedActorId,
            endDateTime = Start.AddMinutes(5)
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(30, created.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(Start.AddMinutes(30), created.GetProperty("endDateTime").GetDateTimeOffset());
        Assert.Equal(actorId, Assert.Single(factory.Audit.Entries).ActorId);
        Assert.NotEqual(suppliedActorId, factory.Audit.Entries[0].ActorId);

        var originalId = created.GetProperty("appointmentId").GetGuid();
        var rescheduledResponse = await client.PostAsJsonAsync($"/api/appointments/{originalId}/reschedule", new
        {
            dentistId = actorId,
            startDateTime = Start.AddHours(1),
            durationMinutes = (int?)null,
            notes = "rescheduled"
        });
        Assert.Equal(HttpStatusCode.OK, rescheduledResponse.StatusCode);
        var replacement = await rescheduledResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(originalId, replacement.GetProperty("rescheduledFromAppointmentId").GetGuid());
        Assert.Equal(30, replacement.GetProperty("durationMinutes").GetInt32());
        var replacementId = replacement.GetProperty("appointmentId").GetGuid();

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/appointments/{replacementId}/confirm", null)).StatusCode);
        var completed = await client.PostAsync($"/api/appointments/{replacementId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var cancelStart = Start.AddHours(2);
        var cancellable = Appointment.Create(Guid.NewGuid(), actorId, cancelStart, 30);
        factory.Repository.Seed(cancellable);
        var cancelled = await client.PostAsync($"/api/appointments/{cancellable.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task AvailabilityAndDentistOptions_ReturnOnlyAuthorizedMinimalData()
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        using var client = factory.CreateAuthenticatedClient(actorId,
            PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsCreate);

        var availability = await client.GetAsync(
            "/api/appointments/availability?from=2030-01-01T09:00:00Z&to=2030-01-01T12:00:00Z&dentistId=" + actorId);
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        var availabilityBody = await availability.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(30, availabilityBody.GetProperty("defaultAppointmentDurationMinutes").GetInt32());

        var dentistsResponse = await client.GetAsync("/api/appointments/dentists");
        Assert.Equal(HttpStatusCode.OK, dentistsResponse.StatusCode);
        var dentists = await dentistsResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(dentists);
        var dentist = Assert.Single(dentists);
        Assert.Equal(factory.Participants.ActiveDentists[0].Id, dentist.GetProperty("id").GetGuid());
        Assert.Equal("Option", dentist.GetProperty("firstName").GetString());
        Assert.False(dentist.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Endpoints_MapConflictsNotFoundAndInvalidRanges()
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        factory.Repository.Seed(Appointment.Create(Guid.NewGuid(), actorId, Start, 30));
        using var client = factory.CreateAuthenticatedClient(actorId,
            PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsCreate,
            PermissionCatalog.AppointmentsUpdate, PermissionCatalog.AppointmentsCancel);

        var conflict = await client.PostAsJsonAsync("/api/appointments", new
        {
            patientId = Guid.NewGuid(), dentistId = actorId, startDateTime = Start
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.DoesNotContain("AppointmentApplicationException", await conflict.Content.ReadAsStringAsync());

        var missing = await client.PostAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/reschedule", new
        {
            dentistId = actorId, startDateTime = Start.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var invalidRange = await client.GetAsync(
            "/api/appointments?from=2030-01-02T00:00:00Z&to=2030-01-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
        var invalidDuration = await client.PostAsJsonAsync("/api/appointments", new
        {
            patientId = Guid.NewGuid(), dentistId = actorId, startDateTime = Start, durationMinutes = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDuration.StatusCode);

        using var invalidRouteId = await client.PostAsync("/api/appointments/not-a-guid/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRouteId.StatusCode);
        using var invalidQueryGuid = await client.GetAsync(
            "/api/appointments?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z&dentistId=not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidQueryGuid.StatusCode);

        using var incompleteBody = await client.PostAsJsonAsync("/api/appointments", new
        {
            patientId = Guid.NewGuid(), dentistId = actorId
        });
        Assert.Equal(HttpStatusCode.BadRequest, incompleteBody.StatusCode);
        using var malformedBody = await client.PostAsync("/api/appointments", new StringContent(
            "{\"patientId\":\"not-a-guid\",\"dentistId\":\"" + actorId + "\",\"startDateTime\":\"2030-01-01T10:00:00Z\"}",
            Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformedBody.StatusCode);
    }

    [Theory]
    [InlineData(AppointmentPatientStatus.NotFound, HttpStatusCode.NotFound)]
    [InlineData(AppointmentPatientStatus.Inactive, HttpStatusCode.BadRequest)]
    public async Task CreateAppointment_MapsPatientAvailability(AppointmentPatientStatus status, HttpStatusCode expected)
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        factory.Participants.PatientStatusFor = id => id == patientId ? status : AppointmentPatientStatus.Active;
        using var client = factory.CreateAuthenticatedClient(actorId,
            PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsCreate);

        using var response = await client.PostAsJsonAsync("/api/appointments", CreateRequest(patientId, actorId));

        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain(status.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(AppointmentDentistStatus.NotFound, HttpStatusCode.NotFound)]
    [InlineData(AppointmentDentistStatus.Inactive, HttpStatusCode.BadRequest)]
    [InlineData(AppointmentDentistStatus.NotDentist, HttpStatusCode.BadRequest)]
    public async Task CreateAppointment_MapsDentistAvailability(AppointmentDentistStatus status, HttpStatusCode expected)
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        factory.Participants.DentistStatusFor = id => id == dentistId ? status : AppointmentDentistStatus.ActiveDentist;
        using var client = factory.CreateAuthenticatedClient(actorId,
            PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsCreate,
            PermissionCatalog.AppointmentsViewAll);

        using var response = await client.PostAsJsonAsync("/api/appointments", CreateRequest(Guid.NewGuid(), dentistId));

        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain(status.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StateTransitionConflict_IsMappedTo409WithoutInternalDetails()
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        var cancelled = Appointment.Create(Guid.NewGuid(), actorId, Start, 30);
        cancelled.Cancel();
        factory.Repository.Seed(cancelled);
        using var client = factory.CreateAuthenticatedClient(actorId,
            PermissionCatalog.AppointmentsView, PermissionCatalog.AppointmentsConfirm);

        using var response = await client.PostAsync($"/api/appointments/{cancelled.Id}/confirm", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("Cannot change appointment", body);
    }

    [Fact]
    public async Task UnexpectedFailure_IsMappedTo500WithoutInternalDetails()
    {
        await using var factory = new AppointmentApiFactory();
        var actorId = Guid.NewGuid();
        factory.Repository.ListFailure = new InvalidOperationException("internal database connection detail");
        using var client = factory.CreateAuthenticatedClient(actorId, PermissionCatalog.AppointmentsView);

        using var response = await client.GetAsync(
            "/api/appointments?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("internal database connection detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    private static object CreateRequest(Guid patientId, Guid dentistId) => new
    {
        patientId,
        dentistId,
        startDateTime = Start
    };

    private sealed class AppointmentApiFactory : WebApplicationFactory<Program>
    {
        private const string TestSigningKey = "dentalpro-appointment-api-test-signing-key-123456789";
        private readonly string? _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DentalPro");
        private readonly string? _previousJwtSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");

        public FakeAppointmentRepository Repository { get; } = new();
        public FakeAppointmentParticipantQuery Participants { get; } = new();
        public FakeAuditWriter Audit { get; } = new();
        private readonly MutablePermissions _permissions = new();

        public AppointmentApiFactory()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DentalPro", "Host=localhost;Database=unused");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        }

        public HttpClient CreateAuthenticatedClient(Guid actorId, params string[] permissions)
        {
            _permissions.Grant(actorId, permissions);
            var client = CreateClient();
            var user = new ApplicationUser { UserName = "appointment-test" };
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, actorId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new Claim(JwtTokenGenerator.SecurityStampFingerprintClaim,
                    JwtTokenGenerator.CreateSecurityStampFingerprint(user.SecurityStamp!))
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var jwt = new JwtSecurityToken("DentalPro", "DentalPro.Api", claims, now, now.AddMinutes(15), credentials);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEffectivePermissionQuery>();
                services.AddSingleton<IEffectivePermissionQuery>(_permissions);
                services.RemoveAll<IUserSessionValidator>();
                services.AddSingleton<IUserSessionValidator, ActiveTestSessionValidator>();
                services.RemoveAll<IAppointmentRepository>();
                services.AddSingleton<IAppointmentRepository>(Repository);
                services.RemoveAll<IAppointmentParticipantQuery>();
                services.AddSingleton<IAppointmentParticipantQuery>(Participants);
                services.RemoveAll<IAppointmentUnitOfWork>();
                services.AddSingleton<IAppointmentUnitOfWork, ImmediateUnitOfWork>();
                services.RemoveAll<IClinicScheduleQuery>();
                services.AddSingleton<IClinicScheduleQuery, FakeClinicScheduleQuery>();
                services.RemoveAll<IAuditEventWriter>();
                services.AddSingleton<IAuditEventWriter>(Audit);
            });
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
    }

    private sealed class MutablePermissions : IEffectivePermissionQuery
    {
        private readonly Dictionary<Guid, HashSet<string>> _grants = [];

        public void Grant(Guid userId, IEnumerable<string> codes) =>
            _grants[userId] = codes.ToHashSet(StringComparer.Ordinal);

        public Task<IReadOnlySet<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_grants.TryGetValue(userId, out var codes)
                ? codes : new HashSet<string>(StringComparer.Ordinal));

        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_grants.TryGetValue(userId, out var codes) && codes.Contains(permissionCode));
    }

    private sealed class ActiveTestSessionValidator : IUserSessionValidator
    {
        public Task<bool> IsCurrentAsync(Guid userId, string securityStampFingerprint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty && !string.IsNullOrWhiteSpace(securityStampFingerprint));
    }

    private sealed class FakeAppointmentRepository : IAppointmentRepository
    {
        private readonly List<Appointment> _appointments = [];
        public Exception? ListFailure { get; set; }
        public void Seed(Appointment appointment) => _appointments.Add(appointment);

        public Task<IReadOnlyList<Appointment>> ListAsync(DateTimeOffset from, DateTimeOffset to, Guid? dentistId,
            CancellationToken cancellationToken = default)
        {
            if (ListFailure is not null) return Task.FromException<IReadOnlyList<Appointment>>(ListFailure);
            return Task.FromResult<IReadOnlyList<Appointment>>(
                _appointments.Where(item => (!dentistId.HasValue || item.DentistId == dentistId)
                    && item.StartDateTime < to && item.EndDateTime > from).ToArray());
        }

        public Task<Appointment?> GetByIdForUpdateAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_appointments.SingleOrDefault(item => item.Id == appointmentId));

        public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Appointment>> GetBlockingAsync(Guid dentistId, Guid? patientId,
            DateTimeOffset from, DateTimeOffset to, Guid? excludingAppointmentId = null,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Appointment>>(
            _appointments.Where(item => item.Id != excludingAppointmentId
                && (item.DentistId == dentistId || (patientId.HasValue && item.PatientId == patientId))
                && item.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Completed
                && item.StartDateTime < to && item.EndDateTime > from).ToArray());

        public void Add(Appointment appointment) => _appointments.Add(appointment);
    }

    private sealed class FakeAppointmentParticipantQuery : IAppointmentParticipantQuery
    {
        public AppointmentDentistOption[] ActiveDentists { get; } =
            [new(Guid.NewGuid(), "Option", "Dentist")];

        public Func<Guid, AppointmentPatientStatus> PatientStatusFor { get; set; } =
            _ => AppointmentPatientStatus.Active;

        public Func<Guid, AppointmentDentistStatus> DentistStatusFor { get; set; } =
            _ => AppointmentDentistStatus.ActiveDentist;

        public Task<AppointmentPatientStatus> GetPatientStatusAsync(Guid patientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PatientStatusFor(patientId));

        public Task<AppointmentDentistStatus> GetDentistStatusAsync(Guid dentistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DentistStatusFor(dentistId));

        public Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetPatientNamesAsync(
            IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AppointmentParticipantName>>(patientIds.ToDictionary(
                id => id, _ => new AppointmentParticipantName("Patient", "Test")));

        public Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetDentistNamesAsync(
            IReadOnlyCollection<Guid> dentistIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AppointmentParticipantName>>(dentistIds.ToDictionary(
                id => id, _ => new AppointmentParticipantName("Dentist", "Test")));

        public Task<IReadOnlyList<AppointmentDentistOption>> ListActiveDentistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentDentistOption>>(ActiveDentists);
    }

    private sealed class FakeClinicScheduleQuery : IClinicScheduleQuery
    {
        private static readonly ClinicScheduleSettings Settings = new("UTC", 30, 15);

        public Task<ClinicScheduleSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task<IReadOnlyList<AppointmentInterval>> GetOpenIntervalsAsync(
            DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentInterval>>([new(from, to)]);

        public Task<ClinicScheduleAvailabilitySnapshot> GetAvailabilitySnapshotAsync(
            DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClinicScheduleAvailabilitySnapshot(Settings, [new(from, to)]));
    }

    private sealed class ImmediateUnitOfWork : IAppointmentUnitOfWork
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class FakeAuditWriter : IAuditEventWriter
    {
        public List<AppointmentAuditEntry> Entries { get; } = [];
        public Task AddAsync(AppointmentAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}

