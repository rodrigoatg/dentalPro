using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;

namespace DentalPro.UnitTests;

public sealed class AppointmentApplicationTests
{
    private static readonly DateTimeOffset Start = new(2030, 2, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAppointments_RestrictsCallerToOwnAgenda()
    {
        var actorId = Guid.NewGuid();
        var otherDentistId = Guid.NewGuid();
        var repository = new FakeAppointmentRepository();
        repository.Seed(CreateAppointment(Guid.NewGuid(), actorId));
        repository.Seed(CreateAppointment(Guid.NewGuid(), otherDentistId));
        var useCase = new ListAppointments(repository, Permissions(AppointmentPermissionCodes.View));

        var result = await useCase.ExecuteAsync(actorId, Query());

        Assert.Single(result);
        Assert.Equal(actorId, repository.LastListDentistId);
    }

    [Fact]
    public async Task ListAppointments_AllowsGeneralAgendaAndNarrowingFilter()
    {
        var actorId = Guid.NewGuid();
        var requestedDentistId = Guid.NewGuid();
        var repository = new FakeAppointmentRepository();
        repository.Seed(CreateAppointment(Guid.NewGuid(), requestedDentistId));
        repository.Seed(CreateAppointment(Guid.NewGuid(), Guid.NewGuid()));
        var useCase = new ListAppointments(
            repository, Permissions(AppointmentPermissionCodes.View, AppointmentPermissionCodes.ViewAll));

        var result = await useCase.ExecuteAsync(actorId, Query(requestedDentistId));

        Assert.Single(result);
        Assert.Equal(requestedDentistId, repository.LastListDentistId);
    }

    [Fact]
    public async Task ListAppointments_OtherDentistFilterCannotExpandOwnScope()
    {
        var actorId = Guid.NewGuid();
        var repository = new FakeAppointmentRepository();
        var useCase = new ListAppointments(repository, Permissions(AppointmentPermissionCodes.View));

        var result = await useCase.ExecuteAsync(actorId, Query(Guid.NewGuid()));

        Assert.Empty(result);
        Assert.Equal(0, repository.ListCallCount);
    }

    [Fact]
    public async Task ListAppointments_RequiresViewPermission()
    {
        var useCase = new ListAppointments(new FakeAppointmentRepository(), Permissions());

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), Query()));
    }

    [Fact]
    public async Task GetAvailability_ReturnsOpenIntervalsAfterSubtractingBlockingAppointments()
    {
        var actorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var repository = new FakeAppointmentRepository();
        repository.Seed(Appointment.Create(patientId, actorId, Start.AddMinutes(30), Start.AddMinutes(60)));
        repository.Seed(Appointment.Create(Guid.NewGuid(), actorId, Start.AddMinutes(60), Start.AddMinutes(75)));
        var schedule = new FakeClinicSchedule([new AppointmentInterval(Start, Start.AddHours(2))]);
        var useCase = new GetAppointmentAvailability(
            repository,
            new FakeParticipants(),
            schedule,
            Permissions(AppointmentPermissionCodes.View));

        var result = await useCase.ExecuteAsync(actorId,
            new AppointmentAvailabilityQuery(Start, Start.AddHours(2), actorId, patientId));

        Assert.Collection(result.AvailableIntervals,
            interval => Assert.Equal(new AppointmentInterval(Start, Start.AddMinutes(30)), interval),
            interval => Assert.Equal(new AppointmentInterval(Start.AddMinutes(75), Start.AddHours(2)), interval));
        Assert.Equal(1, schedule.SnapshotReadCount);
    }

    [Theory]
    [InlineData(AppointmentPatientStatus.NotFound)]
    [InlineData(AppointmentPatientStatus.Inactive)]
    public async Task CreateAppointment_RejectsUnavailablePatient(AppointmentPatientStatus patientStatus)
    {
        var actorId = Guid.NewGuid();
        var participants = new FakeParticipants { PatientStatus = patientStatus };
        var fixture = CreateFixture(actorId, participants: participants);

        await Assert.ThrowsAsync<AppointmentPatientUnavailableException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId)));

        Assert.Empty(fixture.Repository.CommittedAppointments);
    }

    [Theory]
    [InlineData(AppointmentDentistStatus.NotFound)]
    [InlineData(AppointmentDentistStatus.Inactive)]
    [InlineData(AppointmentDentistStatus.NotDentist)]
    public async Task CreateAppointment_RejectsUnavailableDentist(AppointmentDentistStatus dentistStatus)
    {
        var actorId = Guid.NewGuid();
        var participants = new FakeParticipants { DentistStatus = dentistStatus };
        var fixture = CreateFixture(actorId, participants: participants);

        await Assert.ThrowsAsync<AppointmentDentistUnavailableException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId)));

        Assert.Empty(fixture.Repository.CommittedAppointments);
    }

    [Fact]
    public async Task CreateAppointment_CreatesAndAuditsWithinUnitOfWork()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);

        var appointment = await fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId));

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Single(fixture.Repository.CommittedAppointments);
        Assert.Equal(AppointmentAuditActions.Create, Assert.Single(fixture.Audit.CommittedEntries).Action);
        Assert.Equal(actorId, fixture.Audit.CommittedEntries[0].ActorId);
        Assert.Equal(30, appointment.DurationMinutes);
        Assert.Equal(Start.AddMinutes(30), appointment.EndDateTime);
    }

    [Fact]
    public async Task CreateAppointment_UsesCustomDurationEvenWhenItIsNotAnIncrementMultiple()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);
        var appointment = await fixture.Create.ExecuteAsync(actorId,
            new CreateAppointmentCommand(Guid.NewGuid(), actorId, Start, 35));

        Assert.Equal(35, appointment.DurationMinutes);
        Assert.Equal(Start.AddMinutes(35), appointment.EndDateTime);
    }

    [Fact]
    public async Task CreateAppointment_RejectsNonPositiveDuration()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);
        await Assert.ThrowsAsync<AppointmentDurationInvalidException>(() => fixture.Create.ExecuteAsync(actorId,
            new CreateAppointmentCommand(Guid.NewGuid(), actorId, Start, 0)));
        Assert.Empty(fixture.Repository.CommittedAppointments);
    }

    [Fact]
    public async Task CreateAppointment_UsesChangedDefaultOnlyForFutureAppointments()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);
        var first = await fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId));
        fixture.Schedule.Settings = new ClinicScheduleSettings("UTC", 45, 15);
        var second = await fixture.Create.ExecuteAsync(actorId,
            new CreateAppointmentCommand(Guid.NewGuid(), actorId, Start.AddHours(1)));

        Assert.Equal(30, first.DurationMinutes);
        Assert.Equal(45, second.DurationMinutes);
    }

    [Fact]
    public async Task Reschedule_WithoutDurationPreservesExistingEffectiveDuration()
    {
        var actorId = Guid.NewGuid();
        var original = Appointment.Create(Guid.NewGuid(), actorId, Start, 50);
        var fixture = CreateFixture(actorId, appointments: [original]);

        var replacement = await fixture.Reschedule.ExecuteAsync(actorId,
            new RescheduleAppointmentCommand(original.Id, actorId, Start.AddHours(1)));

        Assert.Equal(50, replacement.DurationMinutes);
        Assert.Equal(Start.AddHours(1).AddMinutes(50), replacement.EndDateTime);
    }

    [Fact]
    public void ClinicScheduleTime_RejectsInvalidAndAmbiguousLocalTimes()
    {
        var settings = new ClinicScheduleSettings("America/New_York", 30, 15);
        Assert.Throws<AppointmentLocalTimeInvalidException>(() =>
            ClinicScheduleTime.ResolveLocalStart(new DateTimeOffset(2024, 3, 10, 2, 30, 0, TimeSpan.FromHours(-5)), settings));
        Assert.Throws<AppointmentLocalTimeAmbiguousException>(() =>
            ClinicScheduleTime.ResolveLocalStart(new DateTimeOffset(2024, 11, 3, 1, 30, 0, TimeSpan.FromHours(-4)), settings));
    }

    [Fact]
    public void ClinicScheduleTime_ResolvesLocalWallTimeUsingConfiguredZone()
    {
        var settings = new ClinicScheduleSettings("America/Argentina/Buenos_Aires", 30, 15);
        var local = new DateTimeOffset(2024, 7, 1, 10, 0, 0, TimeSpan.FromHours(-3));

        Assert.Equal(new DateTimeOffset(2024, 7, 1, 13, 0, 0, TimeSpan.Zero),
            ClinicScheduleTime.ResolveLocalStart(local, settings));
        Assert.Throws<AppointmentLocalTimeOffsetMismatchException>(() =>
            ClinicScheduleTime.ResolveLocalStart(new DateTimeOffset(2024, 7, 1, 10, 0, 0, TimeSpan.Zero), settings));
    }

    [Fact]
    public async Task GetAvailability_DistinguishesUnconfiguredAndConfiguredClosedSchedule()
    {
        var actorId = Guid.NewGuid();
        var missing = new FakeClinicSchedule([]) { Configured = false };
        var missingQuery = new GetAppointmentAvailability(new FakeAppointmentRepository(), new FakeParticipants(),
            missing, Permissions(AppointmentPermissionCodes.View));
        await Assert.ThrowsAsync<ClinicScheduleNotConfiguredException>(() => missingQuery.ExecuteAsync(actorId,
            new AppointmentAvailabilityQuery(Start, Start.AddHours(1), actorId)));

        var closed = new FakeClinicSchedule([]);
        var closedQuery = new GetAppointmentAvailability(new FakeAppointmentRepository(), new FakeParticipants(),
            closed, Permissions(AppointmentPermissionCodes.View));
        var availability = await closedQuery.ExecuteAsync(actorId,
            new AppointmentAvailabilityQuery(Start, Start.AddHours(1), actorId));
        Assert.Empty(availability.AvailableIntervals);
    }

    [Fact]
    public async Task CreateAppointment_RejectsOutOfScopeDentist()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(Guid.NewGuid())));

        Assert.Empty(fixture.Repository.CommittedAppointments);
    }

    [Fact]
    public async Task CreateAppointment_RequiresCreatePermissionEvenWithGeneralReadScope()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId, permissions: Permissions(
            AppointmentPermissionCodes.View, AppointmentPermissionCodes.ViewAll));

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId)));

        Assert.Empty(fixture.Repository.CommittedAppointments);
        Assert.Empty(fixture.Audit.CommittedEntries);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("cancel")]
    [InlineData("confirm")]
    [InlineData("complete")]
    public async Task AppointmentActions_RequireTheirOwnPermission(string operationName)
    {
        var actorId = Guid.NewGuid();
        var appointment = Appointment.Create(Guid.NewGuid(), actorId, Start, Start.AddMinutes(30));
        var fixture = CreateFixture(actorId, appointments: [appointment], permissions: Permissions(AppointmentPermissionCodes.View));
        Func<Task> operation = operationName switch
        {
            "create" => () => fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId)),
            "update" => () => fixture.Reschedule.ExecuteAsync(actorId,
                new RescheduleAppointmentCommand(appointment.Id, actorId, Start.AddHours(1))),
            "cancel" => () => fixture.Cancel.ExecuteAsync(actorId, appointment.Id),
            "confirm" => () => fixture.Confirm.ExecuteAsync(actorId, appointment.Id),
            "complete" => () => fixture.Complete.ExecuteAsync(actorId, appointment.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(operationName))
        };

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(operation);

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Empty(fixture.Audit.CommittedEntries);
    }

    [Fact]
    public async Task CreateAppointment_RejectsIntervalOutsideClinicHours()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId,
            openings: [new AppointmentInterval(Start.AddHours(1), Start.AddHours(2))]);

        await Assert.ThrowsAsync<AppointmentOutsideWorkingHoursException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId)));

        Assert.Empty(fixture.Repository.CommittedAppointments);
    }

    [Fact]
    public async Task CreateAppointment_RechecksAvailabilityAndRejectsConflict()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);
        fixture.Repository.Seed(Appointment.Create(
            Guid.NewGuid(), actorId, Start.AddMinutes(10), Start.AddMinutes(20)));

        await Assert.ThrowsAsync<AppointmentAvailabilityConflictException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId)));

        Assert.Single(fixture.Repository.CommittedAppointments);
    }

    [Fact]
    public async Task CreateAppointment_InvalidDomainIntervalDoesNotPersist()
    {
        var actorId = Guid.NewGuid();
        var fixture = CreateFixture(actorId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Create.ExecuteAsync(actorId, CreateCommand(actorId, Start, Start)));

        Assert.Empty(fixture.Repository.CommittedAppointments);
    }

    [Fact]
    public async Task Reschedule_CreatesLinkedReplacementAuditsAndExcludesOriginalFromConflictCheck()
    {
        var actorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var original = Appointment.Create(patientId, actorId, Start, Start.AddMinutes(30));
        var fixture = CreateFixture(actorId, appointments: [original]);

        var replacement = await fixture.Reschedule.ExecuteAsync(actorId,
            new RescheduleAppointmentCommand(original.Id, actorId, Start.AddHours(1), Start.AddHours(1).AddMinutes(30)));

        Assert.Equal(AppointmentStatus.Rescheduled, original.Status);
        Assert.Equal(original.Id, replacement.RescheduledFromAppointmentId);
        Assert.Equal(patientId, replacement.PatientId);
        Assert.Equal(original.Id, fixture.Repository.LastExcludedAppointmentId);
        Assert.Same(original, Assert.Single(fixture.Repository.UpdatedAppointments));
        Assert.Equal(2, fixture.Repository.CommittedAppointments.Count);
        Assert.Equal(AppointmentAuditActions.Reschedule, Assert.Single(fixture.Audit.CommittedEntries).Action);
        Assert.Equal(replacement.Id, fixture.Audit.CommittedEntries[0].RelatedAppointmentId);
    }

    [Fact]
    public async Task Reschedule_AuditFailureRollsBackPersistedAppointmentChanges()
    {
        var actorId = Guid.NewGuid();
        var original = Appointment.Create(Guid.NewGuid(), actorId, Start, Start.AddMinutes(30));
        var fixture = CreateFixture(actorId, appointments: [original]);
        fixture.Audit.ThrowOnAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reschedule.ExecuteAsync(actorId,
            new RescheduleAppointmentCommand(original.Id, actorId, Start.AddHours(1), Start.AddHours(1).AddMinutes(30))));

        Assert.Equal(AppointmentStatus.Scheduled, fixture.Repository.CommittedStatuses[original.Id]);
        Assert.Single(fixture.Repository.CommittedAppointments);
        Assert.Empty(fixture.Audit.CommittedEntries);
    }

    [Fact]
    public async Task Reschedule_RejectsNonOwnerWithoutGeneralScope()
    {
        var actorId = Guid.NewGuid();
        var original = Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMinutes(30));
        var fixture = CreateFixture(actorId, appointments: [original]);

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() => fixture.Reschedule.ExecuteAsync(actorId,
            new RescheduleAppointmentCommand(original.Id, actorId, Start.AddHours(1), Start.AddHours(1).AddMinutes(30))));
    }

    [Fact]
    public async Task Reschedule_CannotMoveAppointmentToDentistOutsideAuthorizedScope()
    {
        var actorId = Guid.NewGuid();
        var original = Appointment.Create(Guid.NewGuid(), actorId, Start, Start.AddMinutes(30));
        var fixture = CreateFixture(actorId, appointments: [original]);

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() => fixture.Reschedule.ExecuteAsync(actorId,
            new RescheduleAppointmentCommand(original.Id, Guid.NewGuid(), Start.AddHours(1))));

        Assert.Equal(AppointmentStatus.Scheduled, original.Status);
        Assert.Single(fixture.Repository.CommittedAppointments);
        Assert.Empty(fixture.Audit.CommittedEntries);
    }

    [Fact]
    public async Task CancelAppointment_ChangesStateAndRecordsAudit()
    {
        var actorId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var historicalCreatedAt = Start.AddYears(-1);
        var appointment = Appointment.Rehydrate(
            id, patientId, actorId, Start, Start.AddMinutes(30), AppointmentStatus.Scheduled,
            "Historical notes", previousId, historicalCreatedAt, historicalCreatedAt);
        var fixture = CreateFixture(actorId, appointments: [appointment]);

        await fixture.Cancel.ExecuteAsync(actorId, appointment.Id);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Same(appointment, Assert.Single(fixture.Repository.UpdatedAppointments));
        Assert.Equal(id, appointment.Id);
        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal(actorId, appointment.DentistId);
        Assert.Equal(Start, appointment.StartDateTime);
        Assert.Equal(Start.AddMinutes(30), appointment.EndDateTime);
        Assert.Equal("Historical notes", appointment.Notes);
        Assert.Equal(previousId, appointment.RescheduledFromAppointmentId);
        Assert.Equal(historicalCreatedAt, appointment.CreatedAt);
        Assert.NotEqual(historicalCreatedAt, appointment.UpdatedAt);
        Assert.Equal(AppointmentAuditActions.Cancel, Assert.Single(fixture.Audit.CommittedEntries).Action);
    }

    [Fact]
    public async Task ConfirmAndComplete_AllowGeneralScopeAndRequireOwnScopeOtherwise()
    {
        var actorId = Guid.NewGuid();
        var otherDentistId = Guid.NewGuid();
        var confirmed = Appointment.Create(Guid.NewGuid(), otherDentistId, Start, Start.AddMinutes(30));
        var completed = Appointment.Create(Guid.NewGuid(), otherDentistId, Start.AddHours(1), Start.AddHours(1).AddMinutes(30));
        var fixture = CreateFixture(actorId,
            appointments: [confirmed, completed],
            permissions: Permissions(AppointmentPermissionCodes.View, AppointmentPermissionCodes.ViewAll,
                AppointmentPermissionCodes.Confirm, AppointmentPermissionCodes.Complete));

        await fixture.Confirm.ExecuteAsync(actorId, confirmed.Id);
        await fixture.Complete.ExecuteAsync(actorId, completed.Id);

        Assert.Equal(AppointmentStatus.Confirmed, confirmed.Status);
        Assert.Equal(AppointmentStatus.Completed, completed.Status);
        Assert.Equal(2, fixture.Audit.CommittedEntries.Count);
    }

    [Fact]
    public async Task ConfirmCompleteAndInvalidTransition_AuditSuccessfulChangesOnly()
    {
        var actorId = Guid.NewGuid();
        var confirmed = Appointment.Create(Guid.NewGuid(), actorId, Start, Start.AddMinutes(30));
        var completed = Appointment.Create(Guid.NewGuid(), actorId, Start.AddHours(1), Start.AddHours(1).AddMinutes(30));
        var cancelled = Appointment.Create(Guid.NewGuid(), actorId, Start.AddHours(2), Start.AddHours(2).AddMinutes(30));
        cancelled.Cancel();
        var fixture = CreateFixture(actorId, appointments: [confirmed, completed, cancelled]);

        await fixture.Confirm.ExecuteAsync(actorId, confirmed.Id);
        await fixture.Complete.ExecuteAsync(actorId, completed.Id);
        await Assert.ThrowsAsync<AppointmentInvalidStateTransitionException>(() =>
            fixture.Confirm.ExecuteAsync(actorId, cancelled.Id));

        Assert.Equal(AppointmentStatus.Confirmed, confirmed.Status);
        Assert.Equal(AppointmentStatus.Completed, completed.Status);
        Assert.Equal(2, fixture.Audit.CommittedEntries.Count);
    }

    [Fact]
    public async Task CancelAppointment_UnknownAppointmentThrowsNotFound()
    {
        var fixture = CreateFixture(Guid.NewGuid());

        await Assert.ThrowsAsync<AppointmentNotFoundException>(() =>
            fixture.Cancel.ExecuteAsync(fixture.ActorId, Guid.NewGuid()));
    }

    private static AppointmentQuery Query(Guid? dentistId = null) =>
        new(Start.AddDays(-1), Start.AddDays(1), dentistId);

    private static CreateAppointmentCommand CreateCommand(
        Guid dentistId,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null) =>
        new(Guid.NewGuid(), dentistId, start ?? Start, end ?? Start.AddMinutes(30));

    private static Appointment CreateAppointment(Guid patientId, Guid dentistId) =>
        Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));

    private static FakePermissions Permissions(params string[] codes) => new(codes);

    private static Fixture CreateFixture(
        Guid actorId,
        FakeParticipants? participants = null,
        AppointmentInterval[]? openings = null,
        Appointment[]? appointments = null,
        FakePermissions? permissions = null)
    {
        var repository = new FakeAppointmentRepository();
        foreach (var appointment in appointments ?? [])
        {
            repository.Seed(appointment);
        }

        var audit = new FakeAuditEventWriter();
        var unitOfWork = new FakeAppointmentUnitOfWork(repository, audit);
        var participantQuery = participants ?? new FakeParticipants();
        var schedule = new FakeClinicSchedule(openings ?? [new AppointmentInterval(Start.AddDays(-1), Start.AddDays(2))]);
        var permissionQuery = permissions ?? Permissions(AppointmentPermissionCodes.View,
            AppointmentPermissionCodes.Create, AppointmentPermissionCodes.Update, AppointmentPermissionCodes.Cancel,
            AppointmentPermissionCodes.Confirm, AppointmentPermissionCodes.Complete);
        return new Fixture(
            actorId,
            repository,
            audit,
            schedule,
            new CreateAppointment(repository, participantQuery, schedule, unitOfWork, audit, permissionQuery),
            new RescheduleAppointment(repository, participantQuery, schedule, unitOfWork, audit, permissionQuery),
            new CancelAppointment(repository, unitOfWork, audit, permissionQuery),
            new ConfirmAppointment(repository, unitOfWork, audit, permissionQuery),
            new CompleteAppointment(repository, unitOfWork, audit, permissionQuery));
    }

    private sealed record Fixture(
        Guid ActorId,
        FakeAppointmentRepository Repository,
        FakeAuditEventWriter Audit,
        FakeClinicSchedule Schedule,
        CreateAppointment Create,
        RescheduleAppointment Reschedule,
        CancelAppointment Cancel,
        ConfirmAppointment Confirm,
        CompleteAppointment Complete);

    private sealed class FakePermissions(params string[] codes) : IEffectivePermissionQuery
    {
        private readonly HashSet<string> _codes = new(codes, StringComparer.Ordinal);

        public Task<IReadOnlySet<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_codes);

        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty && _codes.Contains(permissionCode));
    }

    private sealed class FakeParticipants : IAppointmentParticipantQuery
    {
        public AppointmentPatientStatus PatientStatus { get; init; } = AppointmentPatientStatus.Active;
        public AppointmentDentistStatus DentistStatus { get; init; } = AppointmentDentistStatus.ActiveDentist;

        public Task<AppointmentPatientStatus> GetPatientStatusAsync(Guid patientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PatientStatus);

        public Task<AppointmentDentistStatus> GetDentistStatusAsync(Guid dentistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DentistStatus);

        public Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetPatientNamesAsync(
            IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AppointmentParticipantName>>(patientIds.ToDictionary(
                id => id, _ => new AppointmentParticipantName("Patient", "Test")));

        public Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetDentistNamesAsync(
            IReadOnlyCollection<Guid> dentistIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AppointmentParticipantName>>(dentistIds.ToDictionary(
                id => id, _ => new AppointmentParticipantName("Dentist", "Test")));

        public Task<IReadOnlyList<AppointmentDentistOption>> ListActiveDentistsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentDentistOption>>([]);
    }

    private sealed class FakeClinicSchedule(AppointmentInterval[] openings) : IClinicScheduleQuery
    {
        public bool Configured { get; set; } = true;
        public ClinicScheduleSettings Settings { get; set; } = new("UTC", 30, 15);
        public int SnapshotReadCount { get; private set; }

        public Task<ClinicScheduleSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Configured
            ? Task.FromResult(Settings)
            : Task.FromException<ClinicScheduleSettings>(new ClinicScheduleNotConfiguredException());

        public Task<IReadOnlyList<AppointmentInterval>> GetOpenIntervalsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentInterval>>(openings);

        public Task<ClinicScheduleAvailabilitySnapshot> GetAvailabilitySnapshotAsync(
            DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            SnapshotReadCount++;
            return Configured
                ? Task.FromResult(new ClinicScheduleAvailabilitySnapshot(Settings, openings))
                : Task.FromException<ClinicScheduleAvailabilitySnapshot>(new ClinicScheduleNotConfiguredException());
        }
    }

    private sealed class FakeAppointmentRepository : IAppointmentRepository
    {
        private readonly List<Appointment> _appointments = [];
        private readonly List<Appointment> _pendingAdds = [];
        private readonly Dictionary<Guid, AppointmentStatus> _committedStatuses = [];

        public IReadOnlyList<Appointment> CommittedAppointments => _appointments;
        public List<Appointment> UpdatedAppointments { get; } = [];
        public IReadOnlyDictionary<Guid, AppointmentStatus> CommittedStatuses => _committedStatuses;
        public Guid? LastListDentistId { get; private set; }
        public Guid? LastExcludedAppointmentId { get; private set; }
        public int ListCallCount { get; private set; }

        public void Seed(Appointment appointment)
        {
            _appointments.Add(appointment);
            _committedStatuses[appointment.Id] = appointment.Status;
        }

        public Task<IReadOnlyList<Appointment>> ListAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? dentistId,
            CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            LastListDentistId = dentistId;
            IReadOnlyList<Appointment> found = _appointments
                .Where(item => (!dentistId.HasValue || item.DentistId == dentistId)
                    && item.StartDateTime < to && item.EndDateTime > from)
                .ToArray();
            return Task.FromResult(found);
        }

        public Task<Appointment?> GetByIdForUpdateAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_appointments.SingleOrDefault(item => item.Id == appointmentId));

        public Task<IReadOnlyList<Appointment>> GetBlockingAsync(
            Guid dentistId,
            Guid? patientId,
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? excludingAppointmentId = null,
            CancellationToken cancellationToken = default)
        {
            LastExcludedAppointmentId = excludingAppointmentId;
            IReadOnlyList<Appointment> found = _appointments
                .Concat(_pendingAdds)
                .Where(item => item.Id != excludingAppointmentId
                    && (item.DentistId == dentistId || (patientId.HasValue && item.PatientId == patientId))
                    && item.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Completed
                    && item.StartDateTime < to && item.EndDateTime > from)
                .ToArray();
            return Task.FromResult(found);
        }

        public void Add(Appointment appointment) => _pendingAdds.Add(appointment);

        public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
        {
            UpdatedAppointments.Add(appointment);
            return Task.CompletedTask;
        }

        public void Commit()
        {
            _appointments.AddRange(_pendingAdds);
            _pendingAdds.Clear();
            _committedStatuses.Clear();
            foreach (var appointment in _appointments)
            {
                _committedStatuses[appointment.Id] = appointment.Status;
            }
        }

        public void Rollback() => _pendingAdds.Clear();
    }

    private sealed class FakeAuditEventWriter : IAuditEventWriter
    {
        private readonly List<AppointmentAuditEntry> _pending = [];
        private readonly List<AppointmentAuditEntry> _committed = [];

        public bool ThrowOnAdd { get; set; }
        public IReadOnlyList<AppointmentAuditEntry> CommittedEntries => _committed;

        public Task AddAsync(AppointmentAuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("Simulated audit write failure.");
            }

            _pending.Add(entry);
            return Task.CompletedTask;
        }

        public void Commit()
        {
            _committed.AddRange(_pending);
            _pending.Clear();
        }

        public void Rollback() => _pending.Clear();
    }

    private sealed class FakeAppointmentUnitOfWork(
        FakeAppointmentRepository repository,
        FakeAuditEventWriter audit) : IAppointmentUnitOfWork
    {
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await operation(cancellationToken);
                repository.Commit();
                audit.Commit();
                return result;
            }
            catch
            {
                repository.Rollback();
                audit.Rollback();
                throw;
            }
        }
    }
}
