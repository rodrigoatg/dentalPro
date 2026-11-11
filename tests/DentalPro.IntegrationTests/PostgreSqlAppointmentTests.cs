using DentalPro.Domain.Appointments;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DentalPro.IntegrationTests;

[Collection("Appointment PostgreSQL")]
public sealed class PostgreSqlAppointmentTests
{
    public const string ConnectionStringEnvironmentVariable = "DENTALPRO_APPOINTMENT_TEST_CONNECTION_STRING";

    private static readonly DateTimeOffset Start = new(2041, 8, 12, 10, 0, 0, TimeSpan.Zero);

    [PostgreSqlAppointmentFact]
    public async Task Repository_AddAndLoad_RestoresAllFieldsAndReusesTrackedAggregate()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30), "Initial notes");
        var repository = new AppointmentRepository(context);
        repository.Add(appointment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        repository = new AppointmentRepository(context);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var loaded = await repository.GetByIdForUpdateAsync(appointment.Id);
        var loadedAgain = await repository.GetByIdForUpdateAsync(appointment.Id);
        var listed = await repository.ListAsync(Start.AddMinutes(-1), Start.AddMinutes(31), dentistId);

        Assert.NotNull(loaded);
        Assert.Same(loaded, loadedAgain);
        Assert.Same(loaded, Assert.Single(listed));
        Assert.Equal(appointment.Id, loaded.Id);
        Assert.Equal(patientId, loaded.PatientId);
        Assert.Equal(dentistId, loaded.DentistId);
        Assert.Equal(Start, loaded.StartDateTime);
        Assert.Equal(Start.AddMinutes(30), loaded.EndDateTime);
        Assert.Equal(AppointmentStatus.Scheduled, loaded.Status);
        Assert.Equal("Initial notes", loaded.Notes);
        Assert.Null(loaded.RescheduledFromAppointmentId);
        Assert.Equal(PostgreSqlTimestamp(appointment.CreatedAt), loaded.CreatedAt);
        Assert.Equal(PostgreSqlTimestamp(appointment.UpdatedAt), loaded.UpdatedAt);
        await transaction.CommitAsync();
    }

    [PostgreSqlAppointmentFact]
    public async Task Repository_RoundTripsNonScheduledStatusesAndRescheduleReference()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);

        var confirmed = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(20));
        confirmed.Confirm();
        var completed = Appointment.Create(patientId, dentistId, Start.AddHours(1), Start.AddHours(1).AddMinutes(20));
        completed.Complete();
        var cancelled = Appointment.Create(patientId, dentistId, Start.AddHours(2), Start.AddHours(2).AddMinutes(20));
        cancelled.Cancel();
        var original = Appointment.Create(patientId, dentistId, Start.AddHours(3), Start.AddHours(3).AddMinutes(20));
        var replacement = original.Reschedule(
            dentistId, Start.AddHours(4), Start.AddHours(4).AddMinutes(20), "Replacement");

        var repository = new AppointmentRepository(context);
        repository.Add(confirmed);
        repository.Add(completed);
        repository.Add(cancelled);
        repository.Add(original);
        repository.Add(replacement);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        repository = new AppointmentRepository(context);
        await using var transaction = await context.Database.BeginTransactionAsync();
        Assert.Equal(AppointmentStatus.Confirmed, (await repository.GetByIdForUpdateAsync(confirmed.Id))!.Status);
        Assert.Equal(AppointmentStatus.Completed, (await repository.GetByIdForUpdateAsync(completed.Id))!.Status);
        Assert.Equal(AppointmentStatus.Cancelled, (await repository.GetByIdForUpdateAsync(cancelled.Id))!.Status);
        var loadedOriginal = await repository.GetByIdForUpdateAsync(original.Id);
        var loadedReplacement = await repository.GetByIdForUpdateAsync(replacement.Id);
        Assert.Equal(AppointmentStatus.Rescheduled, loadedOriginal!.Status);
        Assert.Equal(AppointmentStatus.Scheduled, loadedReplacement!.Status);
        Assert.Equal(original.Id, loadedReplacement.RescheduledFromAppointmentId);
        Assert.Equal("Replacement", loadedReplacement.Notes);
        await transaction.CommitAsync();
    }

    [PostgreSqlAppointmentFact]
    public async Task Repository_Update_ChangesStatusAndUpdatedAtButPreservesHistoricalFields()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30), "Original notes");
        var repository = new AppointmentRepository(context);
        repository.Add(appointment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        repository = new AppointmentRepository(context);
        DateTimeOffset updatedAt;
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var reconstructed = await repository.GetByIdForUpdateAsync(appointment.Id);
            Assert.NotNull(reconstructed);
            reconstructed.Confirm();
            updatedAt = reconstructed.UpdatedAt;
            await repository.UpdateAsync(reconstructed);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        context.ChangeTracker.Clear();

        repository = new AppointmentRepository(context);
        await using var verifyTransaction = await context.Database.BeginTransactionAsync();
        var persisted = await repository.GetByIdForUpdateAsync(appointment.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AppointmentStatus.Confirmed, persisted.Status);
        Assert.Equal(patientId, persisted.PatientId);
        Assert.Equal(dentistId, persisted.DentistId);
        Assert.Equal(Start, persisted.StartDateTime);
        Assert.Equal(Start.AddMinutes(30), persisted.EndDateTime);
        Assert.Equal("Original notes", persisted.Notes);
        Assert.Null(persisted.RescheduledFromAppointmentId);
        Assert.Equal(PostgreSqlTimestamp(appointment.CreatedAt), persisted.CreatedAt);
        Assert.Equal(PostgreSqlTimestamp(updatedAt), persisted.UpdatedAt);
        await verifyTransaction.CommitAsync();
    }

    [PostgreSqlAppointmentFact]
    public async Task Repository_Update_RejectsAggregateNotLoadedByThisRepository()
    {
        await using var context = await CreateContextAsync();
        var repository = new AppointmentRepository(context);
        var notTracked = Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMinutes(30));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(notTracked));
        Assert.Empty(context.ChangeTracker.Entries<AppointmentRow>());
    }

    [PostgreSqlAppointmentFact]
    public async Task Repository_QueriesRespectDentistPatientRangeBlockingStatesAndHalfOpenBoundaries()
    {
        await using var context = await CreateContextAsync();
        var (patientId, dentistId) = await AddParticipantsAsync(context);
        var otherPatientId = Guid.NewGuid();
        var otherDentistId = Guid.NewGuid();
        await AddParticipantsAsync(context, otherPatientId, otherDentistId);

        var scheduled = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));
        var confirmedForPatient = Appointment.Create(patientId, otherDentistId, Start.AddMinutes(30), Start.AddMinutes(60));
        confirmedForPatient.Confirm();
        var completedForDentist = Appointment.Create(otherPatientId, dentistId, Start.AddMinutes(60), Start.AddMinutes(90));
        completedForDentist.Complete();
        var cancelled = Appointment.Create(otherPatientId, dentistId, Start.AddMinutes(30), Start.AddMinutes(50));
        cancelled.Cancel();
        var rescheduledOriginal = Appointment.Create(otherPatientId, dentistId, Start.AddMinutes(40), Start.AddMinutes(55));
        var rescheduledReplacement = rescheduledOriginal.Reschedule(
            otherDentistId, Start.AddHours(2), Start.AddHours(2).AddMinutes(15));

        var repository = new AppointmentRepository(context);
        foreach (var appointment in new[]
                 { scheduled, confirmedForPatient, completedForDentist, cancelled, rescheduledOriginal, rescheduledReplacement })
        {
            repository.Add(appointment);
        }

        await context.SaveChangesAsync();

        var agenda = await repository.ListAsync(Start.AddMinutes(10), Start.AddMinutes(30), dentistId);
        Assert.Equal([scheduled.Id], agenda.Select(item => item.Id));

        var adjacentRange = await repository.GetBlockingAsync(
            dentistId, null, Start.AddMinutes(30), Start.AddMinutes(60));
        Assert.Empty(adjacentRange);

        var conflicts = await repository.GetBlockingAsync(
            dentistId, patientId, Start.AddMinutes(10), Start.AddMinutes(80));
        Assert.Equal(
            new[] { scheduled.Id, confirmedForPatient.Id, completedForDentist.Id }.Order(),
            conflicts.Select(item => item.Id).Order());

        var conflictsWithoutScheduled = await repository.GetBlockingAsync(
            dentistId, patientId, Start.AddMinutes(10), Start.AddMinutes(80), scheduled.Id);
        Assert.DoesNotContain(conflictsWithoutScheduled, item => item.Id == scheduled.Id);
        Assert.DoesNotContain(conflicts, item => item.Id == cancelled.Id);
        Assert.DoesNotContain(conflicts, item => item.Id == rescheduledOriginal.Id);
    }

    private static async Task<DentalProDbContext> CreateContextAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"{ConnectionStringEnvironmentVariable} is required.");
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.Database, "dentalpro_appointments_test", StringComparison.OrdinalIgnoreCase)
            || connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException(
                "Appointment persistence tests require the dedicated dentalpro_appointments_test database on loopback.");
        }

        var options = new DbContextOptionsBuilder<DentalProDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var context = new DentalProDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static DateTimeOffset PostgreSqlTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    private static async Task<(Guid PatientId, Guid DentistId)> AddParticipantsAsync(
        DentalProDbContext context,
        Guid? patientId = null,
        Guid? dentistId = null)
    {
        var patient = new PatientRow
        {
            Id = patientId ?? Guid.NewGuid(),
            DocumentType = "TEST",
            DocumentNumber = Guid.NewGuid().ToString("N"),
            FirstName = "Appointment",
            LastName = "Test",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var dentist = new ApplicationUser
        {
            Id = dentistId ?? Guid.NewGuid(),
            UserName = $"dentist-{Guid.NewGuid():N}"
        };
        context.Patients.Add(patient);
        context.Users.Add(dentist);
        await context.SaveChangesAsync();
        return (patient.Id, dentist.Id);
    }
}
