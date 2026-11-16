using DentalPro.Application.ClinicalRecords;
using DentalPro.Domain.Patients;
using DentalPro.Domain.Treatments;
using DentalPro.Infrastructure.Identity;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.Persistence;

public sealed class ClinicalRecordRepository(DentalProDbContext dbContext) : IClinicalRecordRepository
{
    public async Task<MedicalRecord?> GetForUpdateAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.MedicalRecords
            .Include(x => x.Odontogram)
            .SingleOrDefaultAsync(x => x.PatientId == patientId, cancellationToken);
        if (row is null) return null;
        return MedicalRecord.Rehydrate(row.Id, row.PatientId, row.MedicalRecordNumber, row.StartedAt,
            row.LastClinicalActivityAt, row.CreatedAt, row.UpdatedAt,
            Odontogram.Rehydrate(row.Odontogram.Id, row.Odontogram.MedicalRecordId,
                row.Odontogram.CreatedAt, row.Odontogram.UpdatedAt));
    }

    public async Task<ClinicalRecordDetails?> GetAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.MedicalRecords.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .Select(x => new { x.Id, x.PatientId, PatientFirstName = x.Patient.FirstName,
                PatientLastName = x.Patient.LastName, x.MedicalRecordNumber, x.StartedAt, x.LastClinicalActivityAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (record is null) return null;

        var entries = await dbContext.ClinicalEntries.AsNoTracking().AsSplitQuery()
            .Where(x => x.MedicalRecordId == record.Id)
            .Include(x => x.Treatments).ThenInclude(x => x.Treatment).ThenInclude(x => x.Teeth)
            .Include(x => x.Versions).ThenInclude(x => x.Treatments).ThenInclude(x => x.Treatment).ThenInclude(x => x.Teeth)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var peopleIds = entries.Select(x => x.DentistId)
            .Concat(entries.SelectMany(x => x.Treatments.Select(t => t.Treatment.DentistId)))
            .Concat(entries.SelectMany(x => x.Versions.Select(v => v.CreatedBy)))
            .Distinct().ToArray();
        var people = await dbContext.Users.AsNoTracking().Where(x => peopleIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new AppointmentName(x.FirstName, x.LastName), cancellationToken);

        return new ClinicalRecordDetails(record.Id, record.PatientId, record.PatientFirstName, record.PatientLastName,
            record.MedicalRecordNumber,
            record.StartedAt, record.LastClinicalActivityAt,
            entries.Select(entry => new ClinicalEntryDetails(entry.Id, entry.Type, entry.DentistId,
                Name(entry.DentistId).FirstName, Name(entry.DentistId).LastName, entry.AppointmentId,
                entry.OccurredAt, entry.Diagnosis, entry.Notes,
                entry.Versions.OrderBy(v => v.VersionNumber).Select(version => new ClinicalEntryVersionDetails(
                    version.VersionNumber, version.DentistId, version.AppointmentId, version.Type,
                    version.OccurredAt, version.Diagnosis, version.Notes,
                    version.Treatments.Select(link => link.TreatmentId).ToArray(), version.CreatedAt,
                    version.CreatedBy, Name(version.CreatedBy).FirstName, Name(version.CreatedBy).LastName,
                    version.ChangeReason)).ToArray(),
                entry.Treatments.Select(link => MapTreatment(link.Treatment)).ToArray())).ToArray());

        AppointmentName Name(Guid id) => people.TryGetValue(id, out var name) ? name : new AppointmentName("", "");
        ClinicalTreatmentDetails MapTreatment(TreatmentRow row) =>
            new(row.Id, row.DentistId, Name(row.DentistId).FirstName, Name(row.DentistId).LastName,
                row.Type.ToString(), row.OccurredAt, row.Description, row.Priority.ToString(), row.Status.ToString(),
                row.Cost, row.Teeth.OrderBy(x => x.ToothNumber).Select(x => new ClinicalTreatmentToothDetails(
                    x.ToothNumber, x.Status.ToString(), x.CompletedAt, x.Notes, x.ResultingToothState)).ToArray());
    }

    public async Task<bool> HasPatientScopeAsync(Guid patientId, Guid dentistId, CancellationToken cancellationToken = default)
    {
        var recordId = await dbContext.MedicalRecords.AsNoTracking()
            .Where(x => x.PatientId == patientId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (recordId is null) return false;
        var hasAppointment = await dbContext.Appointments.AsNoTracking()
            .AnyAsync(x => x.PatientId == patientId && x.DentistId == dentistId, cancellationToken);
        return hasAppointment || await dbContext.ClinicalEntries.AsNoTracking()
            .AnyAsync(x => x.MedicalRecordId == recordId && x.DentistId == dentistId, cancellationToken);
    }

    public Task<bool> AppointmentBelongsToAsync(Guid appointmentId, Guid patientId, Guid dentistId,
        CancellationToken cancellationToken = default) => dbContext.Appointments.AsNoTracking()
        .AnyAsync(x => x.Id == appointmentId && x.PatientId == patientId && x.DentistId == dentistId,
            cancellationToken);

    public async Task UpdateMedicalRecordAsync(MedicalRecord medicalRecord, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(medicalRecord);
        var row = await dbContext.MedicalRecords.SingleOrDefaultAsync(x => x.Id == medicalRecord.Id, cancellationToken)
            ?? throw new InvalidOperationException("The medical record was not loaded in the current unit of work.");
        row.LastClinicalActivityAt = medicalRecord.LastClinicalActivityAt;
        row.UpdatedAt = medicalRecord.UpdatedAt;
    }

    public Task AddEntryAsync(ClinicalEntry entry, ClinicalEntryVersion initialVersion,
        IReadOnlyList<Treatment> treatments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(initialVersion);
        ArgumentNullException.ThrowIfNull(treatments);
        cancellationToken.ThrowIfCancellationRequested();
        var treatmentRows = treatments.Select(MapTreatment).ToArray();
        var entryRow = new ClinicalEntryRow
        {
            Id = entry.Id, MedicalRecordId = entry.MedicalRecordId, DentistId = entry.DentistId,
            AppointmentId = entry.AppointmentId, Type = entry.Type, OccurredAt = entry.OccurredAt,
            Diagnosis = entry.Diagnosis, Notes = entry.Notes, CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            Treatments = treatmentRows.Select(x => new ClinicalEntryTreatmentRow { ClinicalEntryId = entry.Id, Treatment = x }).ToList(),
            Versions = [new ClinicalEntryVersionRow
            {
                Id = initialVersion.Id, ClinicalEntryId = entry.Id, VersionNumber = initialVersion.VersionNumber,
                MedicalRecordId = initialVersion.MedicalRecordId, DentistId = initialVersion.DentistId,
                AppointmentId = initialVersion.AppointmentId, Type = initialVersion.Type,
                OccurredAt = initialVersion.OccurredAt, Diagnosis = initialVersion.Diagnosis,
                Notes = initialVersion.Notes, CreatedAt = initialVersion.CreatedAt,
                CreatedBy = initialVersion.CreatedBy, ChangeReason = initialVersion.ChangeReason,
                Treatments = initialVersion.TreatmentIds.Select(id => new ClinicalEntryVersionTreatmentRow
                { ClinicalEntryVersionId = initialVersion.Id, Treatment = treatmentRows.Single(x => x.Id == id) }).ToList()
            }]
        };
        dbContext.ClinicalEntries.Add(entryRow);
        return Task.CompletedTask;
    }

    private static TreatmentRow MapTreatment(Treatment treatment) => new()
    {
        Id = treatment.Id, MedicalRecordId = treatment.MedicalRecordId, DentistId = treatment.DentistId,
        Type = treatment.TreatmentType, OccurredAt = treatment.OccurredAt, RecordedAt = treatment.RecordedAt,
        Description = treatment.Description, Priority = treatment.Priority, Status = treatment.Status,
        IsFinalized = treatment.IsFinalized, FinalizedAt = treatment.FinalizedAt, Cost = treatment.Cost,
        CreatedAt = treatment.CreatedAt, UpdatedAt = treatment.UpdatedAt,
        Teeth = treatment.Teeth.Select(tooth => new TreatmentToothRow
        {
            ToothNumber = tooth.ToothNumber.Value, Status = tooth.Status, CompletedAt = tooth.CompletedAt,
            Notes = tooth.Notes, ResultingToothState = tooth.ResultingToothState
        }).ToList()
    };

    private sealed record AppointmentName(string FirstName, string LastName);
}
