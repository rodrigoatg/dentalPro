namespace DentalPro.Domain.Appointments;

public sealed class Appointment
{
    private Appointment(Guid patientId, Guid dentistId, DateTimeOffset start, DateTimeOffset end, string? notes)
    {
        Id = Guid.NewGuid(); PatientId = patientId; DentistId = dentistId;
        StartDateTime = start; EndDateTime = end; Notes = notes;
        Status = AppointmentStatus.Scheduled; CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DentistId { get; private set; }
    public DateTimeOffset StartDateTime { get; private set; }
    public DateTimeOffset EndDateTime { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static Appointment Create(Guid patientId, Guid dentistId, DateTimeOffset startDateTime, DateTimeOffset endDateTime, string? notes = null)
    {
        if (patientId == Guid.Empty) throw new ArgumentException("Patient is required.", nameof(patientId));
        if (dentistId == Guid.Empty) throw new ArgumentException("Dentist is required.", nameof(dentistId));
        if (startDateTime >= endDateTime) throw new ArgumentException("Start must be before end.", nameof(startDateTime));
        return new(patientId, dentistId, startDateTime, endDateTime, notes);
    }
    public void Confirm() => Change(AppointmentStatus.Confirmed, AppointmentStatus.Scheduled);
    public void Complete() => Change(AppointmentStatus.Completed, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
    public void Cancel() => Change(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
    public void MarkRescheduled() => Change(AppointmentStatus.Rescheduled, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed);
    private void Change(AppointmentStatus next, params AppointmentStatus[] allowed)
    {
        if (!allowed.Contains(Status)) throw new InvalidOperationException($"Cannot change appointment in status {Status}.");
        Status = next; UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum AppointmentStatus { Scheduled, Confirmed, Completed, Cancelled, Rescheduled }
