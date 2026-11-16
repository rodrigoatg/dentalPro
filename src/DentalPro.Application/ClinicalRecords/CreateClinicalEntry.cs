using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Domain.Patients;
using DentalPro.Domain.Treatments;

namespace DentalPro.Application.ClinicalRecords;

public sealed class CreateClinicalEntry(
    IClinicalRecordRepository repository,
    IAppointmentParticipantQuery participants,
    IEffectivePermissionQuery permissions,
    IClinicalUnitOfWork unitOfWork,
    IClinicalAuditWriter audit)
{
    public Task<Guid> ExecuteAsync(Guid actorId, CreateClinicalEntryCommand command,
        CancellationToken cancellationToken = default) => unitOfWork.ExecuteAsync(async ct =>
    {
        ArgumentNullException.ThrowIfNull(command);
        if (actorId == Guid.Empty || command.PatientId == Guid.Empty)
            throw new ClinicalRecordNotFoundException();
        await EnsurePermissionAsync(actorId, ClinicalPermissionCodes.EntriesCreate, ct);
        if (command.Treatments.Count > 0 &&
            !await permissions.HasPermissionAsync(actorId, ClinicalPermissionCodes.TreatmentsCreate, ct))
            throw new ClinicalTreatmentPermissionException();

        var patientStatus = await participants.GetPatientStatusAsync(command.PatientId, ct);
        if (patientStatus == AppointmentPatientStatus.NotFound)
            throw new ClinicalRecordNotFoundException();
        if (patientStatus != AppointmentPatientStatus.Active)
            throw new ClinicalPatientUnavailableException();
        if (await participants.GetDentistStatusAsync(actorId, ct) != AppointmentDentistStatus.ActiveDentist)
            throw new ClinicalDentistUnavailableException();

        var canViewAll = await permissions.HasPermissionAsync(actorId, ClinicalPermissionCodes.RecordsViewAll, ct);
        if (!canViewAll && !await repository.HasPatientScopeAsync(command.PatientId, actorId, ct))
            throw new ClinicalRecordOutOfScopeException();
        if (command.AppointmentId is { } appointmentId &&
            !await repository.AppointmentBelongsToAsync(appointmentId, command.PatientId, actorId, ct))
            throw new ClinicalAppointmentMismatchException();

        var medicalRecord = await repository.GetForUpdateAsync(command.PatientId, ct)
            ?? throw new ClinicalRecordNotFoundException();
        var treatments = command.Treatments.Select(item =>
        {
            var toothNumbers = item.ToothNumbers.Select(ToothNumber.Create).ToArray();
            var completedToothNumbers = (item.CompletedToothNumbers ?? []).Select(ToothNumber.Create).ToArray();
            if (item.Status == TreatmentStatus.PartiallyCompleted &&
                (completedToothNumbers.Length == 0 || completedToothNumbers.Length >= toothNumbers.Length ||
                 completedToothNumbers.Distinct().Count() != completedToothNumbers.Length ||
                 completedToothNumbers.Except(toothNumbers).Any()))
                throw new ArgumentException("A partial treatment must have at least one completed and one pending tooth.",
                    nameof(item.CompletedToothNumbers));
            if (item.Status != TreatmentStatus.PartiallyCompleted && completedToothNumbers.Length > 0)
                throw new ArgumentException("Completed tooth values are only valid for a partially completed treatment.",
                    nameof(item.CompletedToothNumbers));

            var initialStatus = item.Status == TreatmentStatus.PartiallyCompleted
                ? TreatmentStatus.InProgress : item.Status;
            var treatment = Treatment.Create(medicalRecord.Id, actorId, item.Type, command.OccurredAt,
                item.Description, toothNumbers, item.Priority, item.Cost, initialStatus);
            foreach (var tooth in completedToothNumbers)
                treatment.CompleteTooth(tooth, completedAt: command.OccurredAt);
            return treatment;
        }).ToArray();
        var entry = ClinicalEntry.Create(medicalRecord.Id, actorId, command.AppointmentId,
            command.Type, command.OccurredAt, command.Diagnosis, command.Notes,
            treatments.Select(treatment => treatment.Id));
        var version = ClinicalEntryVersion.Initial(entry, actorId);

        medicalRecord.RecordClinicalActivity(entry.OccurredAt);
        await repository.UpdateMedicalRecordAsync(medicalRecord, ct);
        await repository.AddEntryAsync(entry, version, treatments, ct);
        await audit.AddAsync(new ClinicalAuditEntry(actorId, "ClinicalEntry", entry.Id,
            "Create", entry.CreatedAt, command.AppointmentId), ct);
        foreach (var treatment in treatments)
            await audit.AddAsync(new ClinicalAuditEntry(actorId, "Treatment", treatment.Id,
                "Create", treatment.CreatedAt, entry.Id), ct);
        return entry.Id;
    }, cancellationToken);

    private async Task EnsurePermissionAsync(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        if (!await permissions.HasPermissionAsync(actorId, permission, cancellationToken))
            throw new ClinicalRecordOutOfScopeException();
    }
}
