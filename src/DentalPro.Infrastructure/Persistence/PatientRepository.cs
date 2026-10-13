using DentalPro.Application.Patients;
using DentalPro.Domain.Patients;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalPro.Infrastructure.Persistence;

public sealed class PatientRepository(DentalProDbContext dbContext) : IPatientRepository
{
    public async Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var medicalRecordNumber = await dbContext.Database
            .SqlQueryRaw<long>("SELECT nextval('public.medical_record_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        patient.MedicalRecord.AssignMedicalRecordNumber(medicalRecordNumber);

        var patientRow = new PatientRow
        {
            Id = patient.Id,
            DocumentType = patient.DocumentType,
            DocumentNumber = patient.DocumentNumber,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Phone = patient.Phone,
            Email = patient.Email,
            Address = patient.Address,
            IsActive = patient.IsActive,
            CreatedAt = patient.CreatedAt,
            UpdatedAt = patient.UpdatedAt
        };

        var medicalRecordRow = new MedicalRecordRow
        {
            Id = patient.MedicalRecord.Id,
            PatientId = patient.MedicalRecord.PatientId,
            MedicalRecordNumber = patient.MedicalRecord.MedicalRecordNumber
                ?? throw new InvalidOperationException("Medical record number was not assigned."),
            StartedAt = patient.MedicalRecord.StartedAt,
            LastClinicalActivityAt = patient.MedicalRecord.LastClinicalActivityAt,
            CreatedAt = patient.MedicalRecord.CreatedAt,
            UpdatedAt = patient.MedicalRecord.UpdatedAt,
            Patient = patientRow
        };

        var odontogramRow = new OdontogramRow
        {
            Id = patient.MedicalRecord.Odontogram.Id,
            MedicalRecordId = patient.MedicalRecord.Odontogram.MedicalRecordId,
            CreatedAt = patient.MedicalRecord.Odontogram.CreatedAt,
            UpdatedAt = patient.MedicalRecord.Odontogram.UpdatedAt,
            MedicalRecord = medicalRecordRow
        };

        patientRow.MedicalRecord = medicalRecordRow;
        medicalRecordRow.Odontogram = odontogramRow;

        dbContext.Patients.Add(patientRow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
