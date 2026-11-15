using DentalPro.Application.Appointments;
using DentalPro.Application.AccessControl;
using DentalPro.Domain.Appointments;

namespace DentalPro.UnitTests;

public sealed class ClinicScheduleApplicationTests
{
    private static readonly Guid AdministratorId = Guid.NewGuid();

    [Fact]
    public async Task CreateAndGet_StoreExplicitConfigurationWithoutInventedHours()
    {
        var repository = new FakeRepository();
        var create = new CreateClinicSchedule(repository, new ImmediateUnitOfWork(), SchedulePermissions());
        var command = new ConfigureClinicScheduleCommand("America/Argentina/Buenos_Aires", 35, 15, []);

        var created = await create.ExecuteAsync(AdministratorId, command);
        var retrieved = await new GetClinicSchedule(repository, SchedulePermissions()).ExecuteAsync(AdministratorId);

        Assert.Equal(created.Id, retrieved.Id);
        Assert.Empty(retrieved.WeeklyIntervals);
        Assert.Equal(35, retrieved.DefaultAppointmentDurationMinutes);
        Assert.True(repository.Added);
    }

    [Fact]
    public async Task Create_RejectsInvalidIanaTimeZoneAndDuplicateConfiguration()
    {
        var repository = new FakeRepository();
        var create = new CreateClinicSchedule(repository, new ImmediateUnitOfWork(), SchedulePermissions());
        await Assert.ThrowsAsync<ClinicScheduleConfigurationException>(() => create.ExecuteAsync(AdministratorId,
            new ConfigureClinicScheduleCommand("Not/A_Real_Zone", 30, 15, [])));
        Assert.False(repository.Added);

        repository.Schedule = ClinicSchedule.Create("UTC", 30, 15);
        await Assert.ThrowsAsync<ClinicScheduleAlreadyConfiguredException>(() => create.ExecuteAsync(AdministratorId,
            new ConfigureClinicScheduleCommand("UTC", 30, 15, [])));
    }

    [Fact]
    public async Task UpdateAndExceptionOperations_ReplaceWeeklyHoursAndPreserveOrRemoveExceptions()
    {
        var date = new DateOnly(2030, 4, 4);
        var repository = new FakeRepository
        {
            Schedule = ClinicSchedule.Create("UTC", 30, 15,
                [new(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
                [new ClinicDateException(date)])
        };
        var uow = new ImmediateUnitOfWork();
        var update = new UpdateClinicSchedule(repository, uow, SchedulePermissions());
        var configured = await update.ExecuteAsync(AdministratorId, new ConfigureClinicScheduleCommand("UTC", 45, 20,
            [new(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(14, 0))]));
        Assert.Equal(45, configured.DefaultAppointmentDurationMinutes);
        Assert.Single(configured.DateExceptions);
        Assert.True(repository.Updated);

        var setException = new SetClinicScheduleException(repository, uow, SchedulePermissions());
        var closed = await setException.ExecuteAsync(AdministratorId, new SetClinicScheduleExceptionCommand(date, []));
        Assert.True(Assert.Single(closed.DateExceptions).IsClosed);
        var special = await setException.ExecuteAsync(AdministratorId, new SetClinicScheduleExceptionCommand(date,
            [(new TimeOnly(11, 0), new TimeOnly(13, 0))]));
        Assert.Equal(new DateOpeningInterval(new TimeOnly(11, 0), new TimeOnly(13, 0)),
            Assert.Single(Assert.Single(special.DateExceptions).Intervals));

        var removed = await new RemoveClinicScheduleException(repository, uow, SchedulePermissions()).ExecuteAsync(AdministratorId, date);
        Assert.Empty(removed.DateExceptions);
    }

    [Fact]
    public async Task GetAndUpdate_RequireInitialConfiguration()
    {
        var repository = new FakeRepository();
        await Assert.ThrowsAsync<ClinicScheduleNotConfiguredException>(() =>
            new GetClinicSchedule(repository, SchedulePermissions()).ExecuteAsync(AdministratorId));
        await Assert.ThrowsAsync<ClinicScheduleNotConfiguredException>(() =>
            new UpdateClinicSchedule(repository, new ImmediateUnitOfWork(), SchedulePermissions()).ExecuteAsync(AdministratorId,
                new ConfigureClinicScheduleCommand("UTC", 30, 15, [])));
    }

    [Fact]
    public async Task ScheduleManagement_RequiresExplicitPermission()
    {
        var create = new CreateClinicSchedule(new FakeRepository(), new ImmediateUnitOfWork(),
            new FakePermissions([]));
        await Assert.ThrowsAsync<AppointmentOutOfScopeException>(() => create.ExecuteAsync(
            AdministratorId, new ConfigureClinicScheduleCommand("UTC", 30, 15, [])));
    }

    private static IEffectivePermissionQuery SchedulePermissions() =>
        new FakePermissions([AppointmentPermissionCodes.ScheduleManage]);

    private sealed class FakePermissions(string[] codes) : IEffectivePermissionQuery
    {
        public Task<IReadOnlySet<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(codes.ToHashSet(StringComparer.Ordinal));
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty && codes.Contains(permissionCode, StringComparer.Ordinal));
    }

    private sealed class FakeRepository : IClinicScheduleRepository
    {
        public ClinicSchedule? Schedule { get; set; }
        public bool Added { get; private set; }
        public bool Updated { get; private set; }

        public Task<ClinicSchedule?> GetForUpdateAsync(CancellationToken cancellationToken = default) => Task.FromResult(Schedule);
        public Task<ClinicSchedule?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Schedule);
        public void Add(ClinicSchedule schedule) { Schedule = schedule; Added = true; }
        public Task UpdateAsync(ClinicSchedule schedule, CancellationToken cancellationToken = default)
        { Schedule = schedule; Updated = true; return Task.CompletedTask; }
    }

    private sealed class ImmediateUnitOfWork : IAppointmentUnitOfWork
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
    }
}
