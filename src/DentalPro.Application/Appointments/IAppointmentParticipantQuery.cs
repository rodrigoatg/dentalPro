namespace DentalPro.Application.Appointments;

public interface IAppointmentParticipantQuery
{
    Task<AppointmentPatientStatus> GetPatientStatusAsync(Guid patientId, CancellationToken cancellationToken = default);

    Task<AppointmentDentistStatus> GetDentistStatusAsync(Guid dentistId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetPatientNamesAsync(
        IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetDentistNamesAsync(
        IReadOnlyCollection<Guid> dentistIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentDentistOption>> ListActiveDentistsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record AppointmentParticipantName(string FirstName, string LastName);

public sealed record AppointmentDentistOption(Guid Id, string FirstName, string LastName);

public enum AppointmentPatientStatus { NotFound, Inactive, Active }

public enum AppointmentDentistStatus { NotFound, Inactive, NotDentist, ActiveDentist }
