using DentalPro.Domain.Patients;
using DentalPro.Domain.Treatments;

namespace DentalPro.Application.ClinicalRecords;

public interface IClinicalRecordRepository
{
    Task<MedicalRecord?> GetForUpdateAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<ClinicalRecordDetails?> GetAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<bool> HasPatientScopeAsync(Guid patientId, Guid dentistId, CancellationToken cancellationToken = default);
    Task<bool> AppointmentBelongsToAsync(Guid appointmentId, Guid patientId, Guid dentistId,
        CancellationToken cancellationToken = default);
    Task UpdateMedicalRecordAsync(MedicalRecord medicalRecord, CancellationToken cancellationToken = default);
    Task AddEntryAsync(ClinicalEntry entry, ClinicalEntryVersion initialVersion,
        IReadOnlyList<Treatment> treatments, CancellationToken cancellationToken = default);
}
