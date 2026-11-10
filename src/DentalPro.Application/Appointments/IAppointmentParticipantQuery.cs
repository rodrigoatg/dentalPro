namespace DentalPro.Application.Appointments;

public interface IAppointmentParticipantQuery
{
    Task<AppointmentPatientStatus> GetPatientStatusAsync(Guid patientId, CancellationToken cancellationToken = default);

    Task<AppointmentDentistStatus> GetDentistStatusAsync(Guid dentistId, CancellationToken cancellationToken = default);
}

public enum AppointmentPatientStatus { NotFound, Inactive, Active }

public enum AppointmentDentistStatus { NotFound, Inactive, NotDentist, ActiveDentist }
