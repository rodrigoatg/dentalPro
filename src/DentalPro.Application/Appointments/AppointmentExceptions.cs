namespace DentalPro.Application.Appointments;

public abstract class AppointmentApplicationException(string message) : Exception(message);

public sealed class AppointmentNotFoundException(Guid appointmentId)
    : AppointmentApplicationException($"Appointment '{appointmentId}' was not found.");

public sealed class AppointmentPatientUnavailableException(Guid patientId, AppointmentPatientStatus status)
    : AppointmentApplicationException($"Patient '{patientId}' is {status}.");

public sealed class AppointmentDentistUnavailableException(Guid dentistId, AppointmentDentistStatus status)
    : AppointmentApplicationException($"Dentist '{dentistId}' is {status}.");

public sealed class AppointmentOutOfScopeException()
    : AppointmentApplicationException("The operation is outside the actor's appointment scope.");

public sealed class AppointmentOutsideWorkingHoursException()
    : AppointmentApplicationException("The appointment interval is outside clinic working hours.");

public sealed class AppointmentAvailabilityConflictException()
    : AppointmentApplicationException("The appointment interval is no longer available.");

public sealed class AppointmentInvalidStateTransitionException(string message)
    : AppointmentApplicationException(message);

public sealed class ClinicScheduleNotConfiguredException()
    : AppointmentApplicationException("The clinic schedule is not configured.");

public sealed class AppointmentQueryValidationException(string message)
    : AppointmentApplicationException(message);
