using DentalPro.Domain.Appointments;

namespace DentalPro.UnitTests;

public sealed class AppointmentTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidDetails_CreatesScheduledAppointment()
    {
        var patientId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        const string notes = "Initial visit";

        var appointment = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30), notes);

        Assert.NotEqual(Guid.Empty, appointment.Id);
        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal(dentistId, appointment.DentistId);
        Assert.Equal(Start, appointment.StartDateTime);
        Assert.Equal(Start.AddMinutes(30), appointment.EndDateTime);
        Assert.Equal(notes, appointment.Notes);
        Assert.Null(appointment.RescheduledFromAppointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(30, appointment.DurationMinutes);
        Assert.Equal(appointment.CreatedAt, appointment.UpdatedAt);
    }

    [Fact]
    public void Create_WithCustomDurationCalculatesEndAndDoesNotRequireIncrementMultiple()
    {
        var appointment = Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), Start, 35);

        Assert.Equal(Start.AddMinutes(35), appointment.EndDateTime);
        Assert.Equal(35, appointment.DurationMinutes);
    }

    [Fact]
    public void Create_WithEmptyPatientId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Appointment.Create(
            Guid.Empty, Guid.NewGuid(), Start, Start.AddMinutes(30)));
    }

    [Fact]
    public void Create_WithEmptyDentistId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Appointment.Create(
            Guid.NewGuid(), Guid.Empty, Start, Start.AddMinutes(30)));
    }

    [Fact]
    public void Create_WithEqualStartAndEnd_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Start, Start));
    }

    [Fact]
    public void Create_WithStartAfterEnd_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Start.AddMinutes(30), Start));
    }

    [Fact]
    public void Create_AllowsConsecutiveAppointmentsToShareBoundary()
    {
        var patientId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        var first = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30));

        var second = Appointment.Create(
            Guid.NewGuid(), dentistId, first.EndDateTime, first.EndDateTime.AddMinutes(30));

        Assert.Equal(first.EndDateTime, second.StartDateTime);
        Assert.Equal(AppointmentStatus.Scheduled, first.Status);
        Assert.Equal(AppointmentStatus.Scheduled, second.Status);
    }

    [Fact]
    public void StatusTransitions_AllowDocumentedTransitions()
    {
        var scheduled = CreateAppointment();
        scheduled.Confirm();
        Assert.Equal(AppointmentStatus.Confirmed, scheduled.Status);

        var completedDirectly = CreateAppointment();
        completedDirectly.Complete();
        Assert.Equal(AppointmentStatus.Completed, completedDirectly.Status);

        var cancelled = CreateAppointment();
        cancelled.Cancel();
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);

        var confirmedThenCompleted = CreateAppointment();
        confirmedThenCompleted.Confirm();
        confirmedThenCompleted.Complete();
        Assert.Equal(AppointmentStatus.Completed, confirmedThenCompleted.Status);

        var confirmedThenCancelled = CreateAppointment();
        confirmedThenCancelled.Confirm();
        confirmedThenCancelled.Cancel();
        Assert.Equal(AppointmentStatus.Cancelled, confirmedThenCancelled.Status);

        var confirmedThenRescheduled = CreateAppointment();
        confirmedThenRescheduled.Confirm();
        var replacement = Reschedule(confirmedThenRescheduled);
        Assert.Equal(AppointmentStatus.Rescheduled, confirmedThenRescheduled.Status);
        Assert.Equal(AppointmentStatus.Scheduled, replacement.Status);
        Assert.Equal(confirmedThenRescheduled.PatientId, replacement.PatientId);
    }

    [Fact]
    public void ConfirmedAppointment_CannotBeConfirmedAgain_AndRemainsUnchanged()
    {
        var appointment = CreateAppointment();
        appointment.Confirm();

        AssertRejectedWithoutMutation(appointment, appointment.Confirm);
    }

    [Fact]
    public void CancelledAppointment_RejectsFurtherStateChangesAndRescheduling()
    {
        var appointment = CreateAppointment();
        appointment.Cancel();

        AssertTerminalOperationsRejected(appointment);
    }

    [Fact]
    public void CompletedAppointment_RejectsFurtherStateChangesAndRescheduling()
    {
        var appointment = CreateAppointment();
        appointment.Confirm();
        appointment.Complete();

        AssertTerminalOperationsRejected(appointment);
    }

    [Fact]
    public void Reschedule_CreatesLinkedReplacementAndPreservesOriginalAppointmentData()
    {
        var patientId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        const string originalNotes = "Original notes";
        var original = Appointment.Create(patientId, dentistId, Start, Start.AddMinutes(30), originalNotes);
        var originalId = original.Id;
        var originalCreatedAt = original.CreatedAt;

        var replacement = original.Reschedule(
            dentistId,
            Start.AddDays(1),
            Start.AddDays(1).AddMinutes(30),
            "Replacement notes");

        Assert.Equal(AppointmentStatus.Rescheduled, original.Status);
        Assert.Equal(originalId, replacement.RescheduledFromAppointmentId);
        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Equal(AppointmentStatus.Scheduled, replacement.Status);
        Assert.Equal(patientId, replacement.PatientId);
        Assert.Equal(dentistId, replacement.DentistId);
        Assert.Equal(Start.AddDays(1), replacement.StartDateTime);
        Assert.Equal(Start.AddDays(1).AddMinutes(30), replacement.EndDateTime);
        Assert.Equal("Replacement notes", replacement.Notes);

        Assert.Equal(originalId, original.Id);
        Assert.Equal(patientId, original.PatientId);
        Assert.Equal(dentistId, original.DentistId);
        Assert.Equal(Start, original.StartDateTime);
        Assert.Equal(Start.AddMinutes(30), original.EndDateTime);
        Assert.Equal(originalNotes, original.Notes);
        Assert.Equal(originalCreatedAt, original.CreatedAt);
        Assert.Null(original.RescheduledFromAppointmentId);
    }

    [Fact]
    public void Reschedule_WithInvalidReplacement_LeavesOriginalUnchanged()
    {
        var original = CreateAppointment();
        var originalId = original.Id;
        var originalPatientId = original.PatientId;
        var originalDentistId = original.DentistId;
        var originalStart = original.StartDateTime;
        var originalEnd = original.EndDateTime;
        var originalNotes = original.Notes;
        var originalStatus = original.Status;
        var originalUpdatedAt = original.UpdatedAt;

        Assert.Throws<ArgumentException>(() => original.Reschedule(
            Guid.NewGuid(),
            Start.AddDays(1),
            Start.AddDays(1)));

        Assert.Equal(originalId, original.Id);
        Assert.Equal(originalPatientId, original.PatientId);
        Assert.Equal(originalDentistId, original.DentistId);
        Assert.Equal(originalStart, original.StartDateTime);
        Assert.Equal(originalEnd, original.EndDateTime);
        Assert.Equal(originalNotes, original.Notes);
        Assert.Equal(originalStatus, original.Status);
        Assert.Equal(originalUpdatedAt, original.UpdatedAt);
        Assert.Null(original.RescheduledFromAppointmentId);
    }

    [Fact]
    public void RescheduledAppointment_RejectsFurtherChangesAndRescheduling()
    {
        var original = CreateAppointment();
        _ = Reschedule(original);

        AssertTerminalOperationsRejected(original);
    }

    private static Appointment CreateAppointment(string? notes = null) =>
        Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMinutes(30), notes);

    private static Appointment Reschedule(Appointment appointment) =>
        appointment.Reschedule(
            appointment.DentistId,
            Start.AddDays(1),
            Start.AddDays(1).AddMinutes(30));

    private static void AssertTerminalOperationsRejected(Appointment appointment)
    {
        AssertRejectedWithoutMutation(appointment, appointment.Confirm);
        AssertRejectedWithoutMutation(appointment, appointment.Complete);
        AssertRejectedWithoutMutation(appointment, appointment.Cancel);
        AssertRejectedWithoutMutation(appointment, () => Reschedule(appointment));
    }

    private static void AssertRejectedWithoutMutation(Appointment appointment, Action operation)
    {
        var id = appointment.Id;
        var patientId = appointment.PatientId;
        var dentistId = appointment.DentistId;
        var startDateTime = appointment.StartDateTime;
        var endDateTime = appointment.EndDateTime;
        var notes = appointment.Notes;
        var status = appointment.Status;
        var createdAt = appointment.CreatedAt;
        var updatedAt = appointment.UpdatedAt;
        var rescheduledFromAppointmentId = appointment.RescheduledFromAppointmentId;

        Assert.Throws<InvalidOperationException>(operation);

        Assert.Equal(id, appointment.Id);
        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal(dentistId, appointment.DentistId);
        Assert.Equal(startDateTime, appointment.StartDateTime);
        Assert.Equal(endDateTime, appointment.EndDateTime);
        Assert.Equal(notes, appointment.Notes);
        Assert.Equal(status, appointment.Status);
        Assert.Equal(createdAt, appointment.CreatedAt);
        Assert.Equal(updatedAt, appointment.UpdatedAt);
        Assert.Equal(rescheduledFromAppointmentId, appointment.RescheduledFromAppointmentId);
    }
}
