namespace DentalPro.Application.Appointments;

public abstract class AppointmentApplicationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class AppointmentNotFoundException(Guid appointmentId)
    : AppointmentApplicationException($"Appointment '{appointmentId}' was not found.");

public sealed class AppointmentPatientUnavailableException(Guid patientId, AppointmentPatientStatus status)
    : AppointmentApplicationException($"Patient '{patientId}' is {status}.")
{
    public Guid PatientId { get; } = patientId;
    public AppointmentPatientStatus Status { get; } = status;
}

public sealed class AppointmentDentistUnavailableException(Guid dentistId, AppointmentDentistStatus status)
    : AppointmentApplicationException($"Dentist '{dentistId}' is {status}.")
{
    public Guid DentistId { get; } = dentistId;
    public AppointmentDentistStatus Status { get; } = status;
}

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

public sealed class ClinicScheduleConfigurationException(string message, Exception? innerException = null)
    : AppointmentApplicationException(message, innerException);

public sealed class AppointmentLocalTimeInvalidException(DateTime localTime)
    : AppointmentApplicationException($"The local appointment time '{localTime:O}' does not exist in the clinic time zone.");

public sealed class AppointmentLocalTimeAmbiguousException(DateTime localTime)
    : AppointmentApplicationException($"The local appointment time '{localTime:O}' is ambiguous in the clinic time zone.");

public sealed class AppointmentLocalTimeOffsetMismatchException(DateTime localTime, TimeSpan expectedOffset)
    : AppointmentApplicationException($"The supplied offset for '{localTime:O}' does not match the clinic time zone (expected {expectedOffset}).");

public sealed class AppointmentDurationInvalidException()
    : AppointmentApplicationException("Appointment duration must be positive and within the supported date range.");

public sealed class ClinicScheduleAlreadyConfiguredException()
    : AppointmentApplicationException("Clinic schedule is already configured.");

public sealed class AppointmentQueryValidationException(string message)
    : AppointmentApplicationException(message);
