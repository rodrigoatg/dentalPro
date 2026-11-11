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
    public async Task ConfirmAndComplete_RequireOwnershipEvenWithGeneralScope()
    {
        var actorId = Guid.NewGuid();
        var otherDentistId = Guid.NewGuid();
        var confirmed = Appointment.Create(Guid.NewGuid(), otherDentistId, Start, Start.AddMinutes(30));
        var completed = Appointment.Create(Guid.NewGuid(), otherDentistId, Start.AddHours(1), Start.AddHours(1).AddMinutes(30));
        var fixture = CreateFixture(actorId,
            appointments: [confirmed, completed],
            permissions: Permissions(AppointmentPermissionCodes.View, AppointmentPermissionCodes.ViewAll));

        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() => fixture.Confirm.ExecuteAsync(actorId, confirmed.Id));
        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() => fixture.Complete.ExecuteAsync(actorId, completed.Id));

        Assert.Equal(AppointmentStatus.Scheduled, confirmed.Status);
        Assert.Equal(AppointmentStatus.Scheduled, completed.Status);
        Assert.Empty(fixture.Audit.CommittedEntries);
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
        var permissionQuery = permissions ?? Permissions(AppointmentPermissionCodes.View);
        return new Fixture(
            actorId,
            repository,
            audit,
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
    }

    private sealed class FakeClinicSchedule(AppointmentInterval[] openings) : IClinicScheduleQuery
    {
        public Task<IReadOnlyList<AppointmentInterval>> GetOpenIntervalsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentInterval>>(openings);
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
