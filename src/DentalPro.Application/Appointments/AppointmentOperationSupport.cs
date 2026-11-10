using DentalPro.Domain.Appointments;

namespace DentalPro.Application.Appointments;

public static class AppointmentAuditActions
{
    public const string Create = "Appointment.Created";
    public const string Reschedule = "Appointment.Rescheduled";
    public const string Cancel = "Appointment.Cancelled";
    public const string Confirm = "Appointment.Confirmed";
    public const string Complete = "Appointment.Completed";
}

internal static class AppointmentStateChanges
{
    public static void Change(Appointment appointment, Action operation)
    {
        try
        {
            operation();
        }
        catch (InvalidOperationException exception)
        {
            throw new AppointmentInvalidStateTransitionException(exception.Message);
        }
    }
}
