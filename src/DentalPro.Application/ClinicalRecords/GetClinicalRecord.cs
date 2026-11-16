using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;

namespace DentalPro.Application.ClinicalRecords;

public sealed class GetClinicalRecord(
    IClinicalRecordRepository repository,
    IAppointmentParticipantQuery participants,
    IEffectivePermissionQuery permissions)
{
    public async Task<ClinicalRecordDetails> ExecuteAsync(Guid actorId, Guid patientId,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || patientId == Guid.Empty) throw new ClinicalRecordNotFoundException();
        if (!await permissions.HasPermissionAsync(actorId, ClinicalPermissionCodes.RecordsView, cancellationToken))
            throw new ClinicalRecordOutOfScopeException();

        var status = await participants.GetPatientStatusAsync(patientId, cancellationToken);
        if (status == AppointmentPatientStatus.NotFound) throw new ClinicalRecordNotFoundException();
        var allRecords = await permissions.HasPermissionAsync(actorId,
            ClinicalPermissionCodes.RecordsViewAll, cancellationToken);
        if (!allRecords)
        {
            var dentist = await participants.GetDentistStatusAsync(actorId, cancellationToken);
            if (dentist != AppointmentDentistStatus.ActiveDentist ||
                !await repository.HasPatientScopeAsync(patientId, actorId, cancellationToken))
                throw new ClinicalRecordOutOfScopeException();
        }

        return await repository.GetAsync(patientId, cancellationToken)
            ?? throw new ClinicalRecordNotFoundException();
    }
}
