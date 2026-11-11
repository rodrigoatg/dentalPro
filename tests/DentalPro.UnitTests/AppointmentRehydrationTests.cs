using DentalPro.Domain.Appointments;

namespace DentalPro.UnitTests;

public sealed class AppointmentRehydrationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rehydrate_PreservesAllPersistedValuesWithoutApplyingTransitions()
    {
        var id = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        var previousAppointmentId = Guid.NewGuid();
        var start = new DateTimeOffset(2025, 4, 5, 9, 15, 0, TimeSpan.Zero);
        var end = start.AddMinutes(45);
        var updatedAt = CreatedAt.AddDays(5);

        var appointment = Appointment.Rehydrate(
            id,
            patientId,
            dentistId,
            start,
            end,
            AppointmentStatus.Confirmed,
            "Persisted notes",
            previousAppointmentId,
            CreatedAt,
            updatedAt);

        Assert.Equal(id, appointment.Id);
        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal(dentistId, appointment.DentistId);
        Assert.Equal(start, appointment.StartDateTime);
        Assert.Equal(end, appointment.EndDateTime);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal("Persisted notes", appointment.Notes);
        Assert.Equal(previousAppointmentId, appointment.RescheduledFromAppointmentId);
        Assert.Equal(CreatedAt, appointment.CreatedAt);
        Assert.Equal(updatedAt, appointment.UpdatedAt);
    }

    [Fact]
    public void Rehydrate_RejectsEmptyAppointmentId()
    {
        Assert.Throws<ArgumentException>(() => Rehydrate(id: Guid.Empty));
    }

    [Fact]
    public void Rehydrate_RejectsEmptyParticipantIds()
    {
        Assert.Throws<ArgumentException>(() => Rehydrate(patientId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Rehydrate(dentistId: Guid.Empty));
    }

    [Fact]
    public void Rehydrate_RejectsInvalidIntervals()
    {
        var start = new DateTimeOffset(2025, 4, 5, 9, 15, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => Rehydrate(start: start, end: start));
        Assert.Throws<ArgumentException>(() => Rehydrate(start: start.AddMinutes(1), end: start));
    }

    [Fact]
    public void Rehydrate_RejectsUnknownStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rehydrate(status: (AppointmentStatus)999));
    }

    [Fact]
    public void Rehydrate_RejectsInvalidReschedulingReference()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Rehydrate(rescheduledFromAppointmentId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Rehydrate(id: id, rescheduledFromAppointmentId: id));
    }

    private static Appointment Rehydrate(
        Guid? id = null,
        Guid? patientId = null,
        Guid? dentistId = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        AppointmentStatus status = AppointmentStatus.Scheduled,
        Guid? rescheduledFromAppointmentId = null) =>
        Appointment.Rehydrate(
            id ?? Guid.NewGuid(),
            patientId ?? Guid.NewGuid(),
            dentistId ?? Guid.NewGuid(),
            start ?? new DateTimeOffset(2025, 4, 5, 9, 15, 0, TimeSpan.Zero),
            end ?? new DateTimeOffset(2025, 4, 5, 10, 0, 0, TimeSpan.Zero),
            status,
            "notes",
            rescheduledFromAppointmentId,
            CreatedAt,
            CreatedAt);
}
