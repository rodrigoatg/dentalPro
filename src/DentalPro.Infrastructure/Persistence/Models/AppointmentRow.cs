using DentalPro.Domain.Appointments;

namespace DentalPro.Infrastructure.Persistence.Models;

public sealed class AppointmentRow
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid DentistId { get; set; }

    public DateTimeOffset StartDateTime { get; set; }

    public DateTimeOffset EndDateTime { get; set; }

    public AppointmentStatus Status { get; set; }

    public string? Notes { get; set; }

    public Guid? RescheduledFromAppointmentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
