namespace DentalPro.Domain.Appointments;

public sealed class Appointment
{
    private Appointment(
        Guid id,
        Guid patientId,
        Guid dentistId,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        AppointmentStatus status,
        string? notes,
        Guid? rescheduledFromAppointmentId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        PatientId = patientId;
        DentistId = dentistId;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        Status = status;
        Notes = notes;
        RescheduledFromAppointmentId = rescheduledFromAppointmentId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Guid DentistId { get; private set; }

    public DateTimeOffset StartDateTime { get; private set; }

    public DateTimeOffset EndDateTime { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public Guid? RescheduledFromAppointmentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Appointment Create(
        Guid patientId,
        Guid dentistId,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        string? notes = null)
    {
        if (patientId == Guid.Empty) throw new ArgumentException("Patient is required.", nameof(patientId));
        if (dentistId == Guid.Empty) throw new ArgumentException("Dentist is required.", nameof(dentistId));
        if (startDateTime >= endDateTime) throw new ArgumentException("Start must be before end.", nameof(startDateTime));

        var now = DateTimeOffset.UtcNow;
        return new(
            Guid.NewGuid(), patientId, dentistId, startDateTime, endDateTime,
            AppointmentStatus.Scheduled, notes, null, now, now);
    }

    /// <summary>Reconstructs persisted state without applying a state transition.</summary>
    internal static Appointment Rehydrate(
        Guid id,
        Guid patientId,
        Guid dentistId,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        AppointmentStatus status,
        string? notes,
        Guid? rescheduledFromAppointmentId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Appointment ID is required.", nameof(id));
        if (patientId == Guid.Empty) throw new ArgumentException("Patient is required.", nameof(patientId));
        if (dentistId == Guid.Empty) throw new ArgumentException("Dentist is required.", nameof(dentistId));
        if (startDateTime >= endDateTime) throw new ArgumentException("Start must be before end.", nameof(startDateTime));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), "Appointment status is invalid.");
        if (rescheduledFromAppointmentId == Guid.Empty)
        {
            throw new ArgumentException("A rescheduled-from ID must be a non-empty ID.", nameof(rescheduledFromAppointmentId));
        }

        if (rescheduledFromAppointmentId == id)
        {
            throw new ArgumentException("An appointment cannot be its own predecessor.", nameof(rescheduledFromAppointmentId));
        }

        return new(
            id, patientId, dentistId, startDateTime, endDateTime,
            status, notes, rescheduledFromAppointmentId, createdAt, updatedAt);
    }

    public void Confirm() => Change(AppointmentStatus.Confirmed, AppointmentStatus.Scheduled);

    public void Complete() => Change(AppointmentStatus.Completed, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);

    public void Cancel() => Change(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);

    public Appointment Reschedule(
        Guid dentistId,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        string? notes = null)
    {
        EnsureCanChange(AppointmentStatus.Rescheduled, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);

        // Validate and create the replacement before changing the original so a failure leaves it untouched.
        var replacement = Create(PatientId, dentistId, startDateTime, endDateTime, notes);
        replacement.RescheduledFromAppointmentId = Id;
        Change(AppointmentStatus.Rescheduled, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
        return replacement;
    }

    private void Change(AppointmentStatus next, params AppointmentStatus[] allowed)
    {
        EnsureCanChange(next, allowed);
        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureCanChange(AppointmentStatus next, params AppointmentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot change appointment from {Status} to {next}.");
        }
    }
}

public enum AppointmentStatus { Scheduled, Confirmed, Completed, Cancelled, Rescheduled }
