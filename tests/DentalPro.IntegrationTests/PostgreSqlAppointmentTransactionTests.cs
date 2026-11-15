using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DentalPro.IntegrationTests;

[Collection("Appointment PostgreSQL")]
public sealed class PostgreSqlAppointmentTransactionTests
{
    private static readonly DateTimeOffset Start = new(2042, 4, 8, 9, 0, 0, TimeSpan.Zero);

    [PostgreSqlAppointmentFact]
    public async Task UnitOfWork_CommitsAppointmentAndAuditTogether()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        var relatedAppointmentId = Guid.NewGuid();
        var repository = new AppointmentRepository(context);
        var writer = new AuditEventWriter(context);
        var unitOfWork = CreateUnitOfWork(context);

        await unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            repository.Add(appointment);
            await writer.AddAsync(new AppointmentAuditEntry(
                dentistId,
                AppointmentAuditActions.Reschedule,
                appointment.Id,
                relatedAppointmentId,
                appointment.CreatedAt), cancellationToken);
            return true;
        });

        context.ChangeTracker.Clear();
        Assert.True(await context.Appointments.AnyAsync(row => row.Id == appointment.Id));
        var audit = await context.AuditEvents.SingleAsync(row => row.EntityId == appointment.Id);
        Assert.Equal(dentistId, audit.ActorId);
        Assert.Equal("Appointment", audit.EntityType);
        Assert.Equal(AppointmentAuditActions.Reschedule, audit.Action);
        Assert.Equal(appointment.Id, audit.EntityId);
        Assert.Equal(relatedAppointmentId, audit.RelatedEntityId);
        Assert.Equal(PostgreSqlTimestamp(appointment.CreatedAt), audit.OccurredAt);
    }

    [PostgreSqlAppointmentFact]
    public async Task UnitOfWork_RollsBackAppointmentAndAuditAndCanReloadAfterFailure()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        var repository = new AppointmentRepository(context);
        var writer = new AuditEventWriter(context);
        var unitOfWork = CreateUnitOfWork(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<bool>(async cancellationToken =>
        {
            repository.Add(appointment);
            await writer.AddAsync(Audit(appointment.Id, dentistId), cancellationToken);
            throw new InvalidOperationException("force rollback");
        }));

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(await context.Appointments.AnyAsync(row => row.Id == appointment.Id));
        Assert.False(await context.AuditEvents.AnyAsync(row => row.EntityId == appointment.Id));

        var committed = Appointment.Create(patientId, dentistId, Start.AddHours(1), Start.AddMinutes(90));
        await unitOfWork.ExecuteAsync<bool>(cancellationToken =>
        {
            repository.Add(committed);
            return Task.FromResult(true);
        });

        Assert.True(await context.Appointments.AnyAsync(row => row.Id == committed.Id));
    }

    [PostgreSqlAppointmentFact]
    public async Task ExclusionViolationAtDeferredCommit_TranslatesAndRollsBackAudit()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var existing = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        context.Appointments.Add(ToRow(existing));
        await context.SaveChangesAsync();

        var conflict = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        var repository = new AppointmentRepository(context);
        var writer = new AuditEventWriter(context);
        var unitOfWork = CreateUnitOfWork(context);

        await Assert.ThrowsAsync<AppointmentAvailabilityConflictException>(() => unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            repository.Add(conflict);
            await writer.AddAsync(Audit(conflict.Id, dentistId), cancellationToken);
            return true;
        }));

        Assert.False(await context.Appointments.AnyAsync(row => row.Id == conflict.Id));
        Assert.False(await context.AuditEvents.AnyAsync(row => row.EntityId == conflict.Id));
    }

    [PostgreSqlAppointmentFact]
    public async Task RescheduleConflict_RollsBackOriginalReplacementAndAudit()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var original = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        var blocker = Appointment.Create(patientId, dentistId, Start.AddHours(1), Start.AddMinutes(90));
        context.Appointments.AddRange(ToRow(original), ToRow(blocker));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new AppointmentRepository(context);
        var writer = new AuditEventWriter(context);
        var unitOfWork = CreateUnitOfWork(context);
        var replacementId = Guid.Empty;

        await Assert.ThrowsAsync<AppointmentAvailabilityConflictException>(() => unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var loadedOriginal = await repository.GetByIdForUpdateAsync(original.Id, cancellationToken);
            Assert.NotNull(loadedOriginal);
            var replacement = loadedOriginal.Reschedule(
                dentistId, Start.AddHours(1), Start.AddMinutes(90), "Replacement");
            replacementId = replacement.Id;
            await repository.UpdateAsync(loadedOriginal, cancellationToken);
            repository.Add(replacement);
            await writer.AddAsync(new AppointmentAuditEntry(
                dentistId,
                AppointmentAuditActions.Reschedule,
                loadedOriginal.Id,
                replacement.Id,
                DateTimeOffset.UtcNow), cancellationToken);
            return replacement;
        }));

        context.ChangeTracker.Clear();
        var persistedOriginal = await context.Appointments.AsNoTracking().SingleAsync(row => row.Id == original.Id);
        Assert.Equal(AppointmentStatus.Scheduled, persistedOriginal.Status);
        Assert.DoesNotContain(await context.Appointments.AsNoTracking().ToListAsync(), row => row.Id == replacementId);
        Assert.False(await context.AuditEvents.AnyAsync(row => row.EntityId == original.Id));
    }

    [PostgreSqlAppointmentFact]
    public async Task Reschedule_CanMoveAnAppointmentWithoutAnIntermediateExclusionConflict()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var original = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        context.Appointments.Add(ToRow(original));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new AppointmentRepository(context);
        var writer = new AuditEventWriter(context);
        var unitOfWork = CreateUnitOfWork(context);
        var replacement = await unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var loadedOriginal = await repository.GetByIdForUpdateAsync(original.Id, cancellationToken);
            Assert.NotNull(loadedOriginal);
            var next = loadedOriginal.Reschedule(
                dentistId, Start.AddHours(1), Start.AddMinutes(90), "Moved");
            await repository.UpdateAsync(loadedOriginal, cancellationToken);
            repository.Add(next);
            await writer.AddAsync(new AppointmentAuditEntry(
                dentistId,
                AppointmentAuditActions.Reschedule,
                loadedOriginal.Id,
                next.Id,
                DateTimeOffset.UtcNow), cancellationToken);
            return next;
        });

        context.ChangeTracker.Clear();
        Assert.Equal(AppointmentStatus.Rescheduled,
            await context.Appointments.AsNoTracking().Where(row => row.Id == original.Id).Select(row => row.Status).SingleAsync());
        var persistedReplacement = await context.Appointments.AsNoTracking().SingleAsync(row => row.Id == replacement.Id);
        Assert.Equal(original.Id, persistedReplacement.RescheduledFromAppointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, persistedReplacement.Status);
        var audit = await context.AuditEvents.AsNoTracking().SingleAsync(row => row.EntityId == original.Id);
        Assert.Equal(replacement.Id, audit.RelatedEntityId);
    }

    [PostgreSqlAppointmentFact]
    public async Task Exclusions_RejectDentistAndPatientOverlapButAllowAdjacentAndNonBlockingStates()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var (otherPatientId, otherDentistId) = await AddParticipantsAsync(context);
        var repository = new AppointmentRepository(context);
        var unitOfWork = CreateUnitOfWork(context);

        var dentistBlocker = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        await unitOfWork.ExecuteAsync<bool>(_ =>
        {
            repository.Add(dentistBlocker);
            return Task.FromResult(true);
        });
        var dentistOverlap = Appointment.Create(otherPatientId, dentistId, Start, Start.AddMinutes(30));
        await AssertConflictAsync(unitOfWork, repository, dentistOverlap);

        var patientOverlap = Appointment.Create(patientId, otherDentistId, Start, Start.AddMinutes(30));
        await AssertConflictAsync(unitOfWork, repository, patientOverlap);

        var adjacent = Appointment.Create(patientId, dentistId, Start.AddMinutes(30), Start.AddMinutes(60));
        await unitOfWork.ExecuteAsync<bool>(_ =>
        {
            repository.Add(adjacent);
            return Task.FromResult(true);
        });

        var cancelled = Appointment.Create(otherPatientId, otherDentistId, Start.AddHours(2), Start.AddMinutes(150));
        cancelled.Cancel();
        var rescheduled = Appointment.Create(otherPatientId, otherDentistId, Start.AddHours(2), Start.AddMinutes(150));
        var replacement = rescheduled.Reschedule(
            otherDentistId, Start.AddHours(3), Start.AddMinutes(210));
        await unitOfWork.ExecuteAsync<bool>(_ =>
        {
            repository.Add(cancelled);
            repository.Add(rescheduled);
            repository.Add(replacement);
            return Task.FromResult(true);
        });
    }

    [PostgreSqlAppointmentFact]
    public async Task Exclusions_CompletedAppointmentsRemainBlocking()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var completed = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        completed.Complete();
        context.Appointments.Add(ToRow(completed));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var conflicting = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        await AssertConflictAsync(CreateUnitOfWork(context), new AppointmentRepository(context), conflicting);
    }

    [PostgreSqlAppointmentFact]
    public async Task Exclusions_ConfirmedAppointmentsRemainBlocking()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var confirmed = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        confirmed.Confirm();
        context.Appointments.Add(ToRow(confirmed));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var conflicting = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        await AssertConflictAsync(CreateUnitOfWork(context), new AppointmentRepository(context), conflicting);
    }

    [PostgreSqlAppointmentFact]
    public async Task RowLock_WaitsAndReloadsTheCommittedStateBeforeUpdating()
    {
        await using var seedContext = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(seedContext);
        var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        seedContext.Appointments.Add(ToRow(appointment));
        await seedContext.SaveChangesAsync();

        await using var firstContext = await CreateContextAsync();
        await using var secondContext = await CreateContextAsync();
        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
        await using var secondTransaction = await secondContext.Database.BeginTransactionAsync();
        var firstRepository = new AppointmentRepository(firstContext);
        var secondRepository = new AppointmentRepository(secondContext);
        var first = await firstRepository.GetByIdForUpdateAsync(appointment.Id);
        Assert.NotNull(first);
        first.Confirm();
        await firstRepository.UpdateAsync(first);
        await firstContext.SaveChangesAsync();

        var queryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRead = Task.Run(async () =>
        {
            queryStarted.SetResult();
            return await secondRepository.GetByIdForUpdateAsync(appointment.Id);
        });
        await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.False(secondRead.IsCompleted);

        await firstTransaction.CommitAsync();
        var refreshed = await secondRead.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(refreshed);
        Assert.Equal(AppointmentStatus.Confirmed, refreshed.Status);
        refreshed.Cancel();
        await secondRepository.UpdateAsync(refreshed);
        await secondContext.SaveChangesAsync();
        await secondTransaction.CommitAsync();

        Assert.Equal(AppointmentStatus.Cancelled,
            await seedContext.Appointments.Where(row => row.Id == appointment.Id).Select(row => row.Status).SingleAsync());
    }

    [PostgreSqlAppointmentFact]
    public async Task ConcurrentReservations_AllowOnlyOneCommitAndReturnControlledConflict()
    {
        await using var seedContext = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(seedContext);
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;

        async Task<bool> ReserveAsync()
        {
            await using var context = await CreateContextAsync();
            var repository = new AppointmentRepository(context);
            var writer = new AuditEventWriter(context);
            var unitOfWork = CreateUnitOfWork(context);
            var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));

            return await unitOfWork.ExecuteAsync(async cancellationToken =>
            {
                Assert.Empty(await repository.GetBlockingAsync(
                    dentistId, patientId, Start, Start.AddMinutes(30), cancellationToken: cancellationToken));
                if (Interlocked.Increment(ref arrivals) == 2)
                {
                    barrier.SetResult();
                }

                await barrier.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                repository.Add(appointment);
                await writer.AddAsync(Audit(appointment.Id, dentistId), cancellationToken);
                return true;
            });
        }

        async Task<bool> TryReserveAsync()
        {
            try
            {
                return await ReserveAsync();
            }
            catch (AppointmentAvailabilityConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryReserveAsync(), TryReserveAsync());
        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Equal(1, await seedContext.Appointments.CountAsync(row => row.DentistId == dentistId));
        Assert.Equal(1, await seedContext.AuditEvents.CountAsync(row => row.ActorId == dentistId));
    }

    private static async Task AssertConflictAsync(
        IAppointmentUnitOfWork unitOfWork,
        AppointmentRepository repository,
        Appointment appointment)
    {
        await Assert.ThrowsAsync<AppointmentAvailabilityConflictException>(() => unitOfWork.ExecuteAsync<bool>(_ =>
        {
            repository.Add(appointment);
            return Task.FromResult(true);
        }));
    }

    private static IAppointmentUnitOfWork CreateUnitOfWork(DentalProDbContext context) =>
        new AppointmentUnitOfWork(context, NullLogger<AppointmentUnitOfWork>.Instance);

    private static AppointmentAuditEntry Audit(Guid appointmentId, Guid actorId) => new(
        actorId,
        AppointmentAuditActions.Create,
        appointmentId,
        null,
        DateTimeOffset.UtcNow);

    private static AppointmentRow ToRow(Appointment appointment) => new()
    {
        Id = appointment.Id,
        PatientId = appointment.PatientId,
        DentistId = appointment.DentistId,
        StartDateTime = appointment.StartDateTime,
        EndDateTime = appointment.EndDateTime,
        DurationMinutes = appointment.DurationMinutes,
        Status = appointment.Status,
        Notes = appointment.Notes,
        RescheduledFromAppointmentId = appointment.RescheduledFromAppointmentId,
        CreatedAt = appointment.CreatedAt,
        UpdatedAt = appointment.UpdatedAt
    };

    private static DateTimeOffset PostgreSqlTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    private static async Task<DentalProDbContext> CreateContextAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgreSqlAppointmentTests.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The isolated PostgreSQL test connection string is required.");
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.Database, "dentalpro_appointments_test", StringComparison.OrdinalIgnoreCase)
            || connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException(
                "Appointment PostgreSQL tests require the dedicated dentalpro_appointments_test database on loopback.");
        }

        var options = new DbContextOptionsBuilder<DentalProDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var context = new DentalProDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static async Task<(Guid PatientId, Guid DentistId)> AddParticipantsAsync(DentalProDbContext context)
    {
        var patient = new PatientRow
        {
            Id = Guid.NewGuid(),
            DocumentType = "TEST",
            DocumentNumber = Guid.NewGuid().ToString("N"),
            FirstName = "Appointment",
            LastName = "Transaction Test",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var dentist = new ApplicationUser { UserName = $"dentist-{Guid.NewGuid():N}" };
        context.Patients.Add(patient);
        context.Users.Add(dentist);
        await context.SaveChangesAsync();
        return (patient.Id, dentist.Id);
    }
}
