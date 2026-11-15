using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DentalPro.IntegrationTests;

[Collection("Appointment PostgreSQL")]
public sealed class PostgreSqlClinicScheduleTests
{
    [PostgreSqlAppointmentFact]
    public async Task Persistence_RoundTripsWeeklyHoursExceptionsAndResolvesUtcIntervals()
    {
        await using var context = await CreateContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var schedule = ClinicSchedule.Create("UTC", 30, 15,
        [
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(18, 0))
        ],
        [
            new(new DateOnly(2041, 8, 13), [(new TimeOnly(10, 0), new TimeOnly(12, 0))]),
            new(new DateOnly(2041, 8, 15))
        ]);
        var persistence = new ClinicSchedulePersistence(context);
        persistence.Add(schedule);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var query = new ClinicSchedulePersistence(context);
        var settings = await query.GetSettingsAsync();
        Assert.Equal("UTC", settings.TimeZoneId);
        Assert.Equal(30, settings.DefaultAppointmentDurationMinutes);
        Assert.Equal(15, settings.StartIncrementMinutes);
        var tuesday = await query.GetOpenIntervalsAsync(Utc(2041, 8, 12, 8), Utc(2041, 8, 12, 14));
        var specialWednesday = await query.GetOpenIntervalsAsync(Utc(2041, 8, 13, 8), Utc(2041, 8, 13, 14));
        var closedFriday = await query.GetOpenIntervalsAsync(Utc(2041, 8, 15, 8), Utc(2041, 8, 15, 14));
        Assert.Equal([new AppointmentInterval(Utc(2041, 8, 12, 9), Utc(2041, 8, 12, 13))], tuesday);
        Assert.Equal([new AppointmentInterval(Utc(2041, 8, 13, 10), Utc(2041, 8, 13, 12))], specialWednesday);
        Assert.Empty(closedFriday);
        await transaction.RollbackAsync();
    }

    [PostgreSqlAppointmentFact]
    public async Task PostgreSql_DeferredExclusionRejectsOverlappingOpeningIntervals()
    {
        await using var context = await CreateContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var scheduleId = Guid.NewGuid();
        context.ClinicSchedules.Add(new DentalPro.Infrastructure.Persistence.Models.ClinicScheduleRow
        {
            Id = scheduleId, SingletonKey = 1, TimeZoneId = "UTC",
            DefaultAppointmentDurationMinutes = 30, StartIncrementMinutes = 15
        });
        context.ClinicWeeklyIntervals.AddRange(
            new DentalPro.Infrastructure.Persistence.Models.ClinicWeeklyIntervalRow
            {
                Id = Guid.NewGuid(), ClinicScheduleId = scheduleId, DayOfWeek = (short)DayOfWeek.Monday,
                Start = new TimeOnly(9, 0), End = new TimeOnly(11, 0)
            },
            new DentalPro.Infrastructure.Persistence.Models.ClinicWeeklyIntervalRow
            {
                Id = Guid.NewGuid(), ClinicScheduleId = scheduleId, DayOfWeek = (short)DayOfWeek.Monday,
                Start = new TimeOnly(10, 0), End = new TimeOnly(12, 0)
            });
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal("23P01", exception.SqlState);
    }

    [PostgreSqlAppointmentFact]
    public async Task Query_RejectsNonexistentAndAmbiguousOpeningBoundariesAtDstTransitions()
    {
        await using var context = await CreateContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var schedule = ClinicSchedule.Create("America/New_York", 30, 15, dateExceptions:
        [
            new(new DateOnly(2024, 3, 10), [(new TimeOnly(2, 15), new TimeOnly(4, 0))]),
            new(new DateOnly(2024, 11, 3), [(new TimeOnly(1, 30), new TimeOnly(3, 0))])
        ]);
        var persistence = new ClinicSchedulePersistence(context);
        persistence.Add(schedule);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ClinicScheduleConfigurationException>(() => persistence.GetOpenIntervalsAsync(
            new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2024, 3, 11, 0, 0, 0, TimeSpan.FromHours(-4))));
        await Assert.ThrowsAsync<ClinicScheduleConfigurationException>(() => persistence.GetOpenIntervalsAsync(
            new DateTimeOffset(2024, 11, 3, 0, 0, 0, TimeSpan.FromHours(-4)),
            new DateTimeOffset(2024, 11, 4, 0, 0, 0, TimeSpan.FromHours(-5))));
        await transaction.RollbackAsync();
    }

    [PostgreSqlAppointmentFact]
    public async Task SharedScheduleReadLock_BlocksConfigurationUpdateUntilBookingUnitOfWorkEnds()
    {
        var connectionString = GetIsolatedConnectionString();
        await using (var setup = await CreateContextAsync(connectionString))
        {
            await setup.Database.ExecuteSqlRawAsync("DELETE FROM \"ClinicSchedules\"");
            setup.ClinicSchedules.Add(new ClinicScheduleRowForTest().Create());
            await setup.SaveChangesAsync();
        }

        await using var bookingContext = await CreateContextAsync(connectionString);
        await using var configurationContext = await CreateContextAsync(connectionString);
        await using var bookingTransaction = await bookingContext.Database.BeginTransactionAsync();
        await using var configurationTransaction = await configurationContext.Database.BeginTransactionAsync();
        var query = new ClinicSchedulePersistence(bookingContext);
        var settings = await query.GetSettingsAsync();
        Assert.Equal(30, settings.DefaultAppointmentDurationMinutes);

        var configure = configurationContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"ClinicSchedules\" SET \"DefaultAppointmentDurationMinutes\" = 45 WHERE \"SingletonKey\" = 1");
        await Task.Delay(150);
        Assert.False(configure.IsCompleted);

        await bookingTransaction.CommitAsync();
        await configure.WaitAsync(TimeSpan.FromSeconds(10));
        await configurationTransaction.CommitAsync();
        await configurationContext.Database.ExecuteSqlRawAsync("DELETE FROM \"ClinicSchedules\"");
    }

    [PostgreSqlAppointmentFact]
    public async Task Repository_RefreshesTrackedScheduleAfterTransactionAndRequiresCurrentLockForUpdate()
    {
        var connectionString = GetIsolatedConnectionString();
        await using var context = await CreateContextAsync(connectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"ClinicSchedules\"");
        var persistence = new ClinicSchedulePersistence(context);
        persistence.Add(ClinicSchedule.Create("UTC", 30, 15));
        await context.SaveChangesAsync();

        ClinicSchedule? previous = null;
        await using (var firstTransaction = await context.Database.BeginTransactionAsync())
        {
            previous = await persistence.GetForUpdateAsync();
            await firstTransaction.CommitAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.UpdateAsync(
            previous!.Update("UTC", 60, 15, [], [])));

        await using (var external = await CreateContextAsync(connectionString))
        {
            await external.Database.ExecuteSqlRawAsync(
                "UPDATE \"ClinicSchedules\" SET \"DefaultAppointmentDurationMinutes\" = 45 WHERE \"SingletonKey\" = 1");
        }

        await using (var secondTransaction = await context.Database.BeginTransactionAsync())
        {
            var refreshed = await persistence.GetForUpdateAsync();
            Assert.NotNull(refreshed);
            Assert.Equal(45, refreshed.DefaultDurationMinutes);
            await persistence.UpdateAsync(refreshed.Update("UTC", 60, 15, [], []));
            await context.SaveChangesAsync();
            await secondTransaction.RollbackAsync();
        }

        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"ClinicSchedules\"");
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    private static async Task<DentalProDbContext> CreateContextAsync(string? suppliedConnectionString = null)
    {
        var connectionString = suppliedConnectionString ?? GetIsolatedConnectionString();
        var options = new DbContextOptionsBuilder<DentalProDbContext>().UseNpgsql(connectionString).Options;
        var context = new DentalProDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static string GetIsolatedConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(PostgreSqlAppointmentTests.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("An isolated PostgreSQL test connection is required.");
        var parsed = new NpgsqlConnectionStringBuilder(value);
        if (!string.Equals(parsed.Database, "dentalpro_appointments_test", StringComparison.OrdinalIgnoreCase)
            || parsed.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Clinic schedule tests require dentalpro_appointments_test on loopback.");
        return value;
    }

    private sealed class ClinicScheduleRowForTest
    {
        public DentalPro.Infrastructure.Persistence.Models.ClinicScheduleRow Create() => new()
        {
            Id = Guid.NewGuid(), SingletonKey = 1, TimeZoneId = "UTC",
            DefaultAppointmentDurationMinutes = 30, StartIncrementMinutes = 15
        };
    }
}
