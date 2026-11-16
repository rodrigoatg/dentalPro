using DentalPro.Application.Patients;
using DentalPro.Domain.Patients;
using DentalPro.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicatePatientDocument(exception))
        {
            throw new PatientDocumentAlreadyExistsException();
        }
    }

    public Task<IReadOnlyList<PatientListItem>> ListAsync(CancellationToken cancellationToken = default) =>
        SearchAsync(null, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        dbContext.Patients.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientListItem>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var terms = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var item in terms)
            {
                var escaped = item.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("%", "\\%", StringComparison.Ordinal)
                    .Replace("_", "\\_", StringComparison.Ordinal);
                var pattern = $"%{escaped}%";
                query = query.Where(patient =>
                    EF.Functions.ILike(patient.FirstName, pattern, "\\") ||
                    EF.Functions.ILike(patient.LastName, pattern, "\\") ||
                    EF.Functions.ILike(patient.DocumentNumber, pattern, "\\") ||
                    EF.Functions.ILike(patient.DocumentType, pattern, "\\"));
            }
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(patient => patient.CreatedAt)
            .ThenBy(patient => patient.Id)
            .Select(patient => new PatientListItem(
                patient.Id,
                patient.DocumentType,
                patient.DocumentNumber,
                patient.FirstName,
                patient.LastName,
                patient.DateOfBirth,
                patient.Phone,
                patient.Email,
                patient.IsActive,
                patient.CreatedAt,
                patient.MedicalRecord.MedicalRecordNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<PatientDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Patients.AsNoTracking()
            .Where(patient => patient.Id == id)
            .Select(patient => new PatientDetails(
                patient.Id, patient.DocumentType, patient.DocumentNumber, patient.FirstName, patient.LastName,
                patient.DateOfBirth, patient.Phone, patient.Email, patient.Address, patient.IsActive,
                patient.CreatedAt, patient.UpdatedAt, patient.MedicalRecord.MedicalRecordNumber))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task UpdateAsync(Guid id, UpdatePatientData data, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Patients.SingleOrDefaultAsync(patient => patient.Id == id, cancellationToken)
            ?? throw new PatientNotFoundException();
        row.DocumentType = data.DocumentType;
        row.DocumentNumber = data.DocumentNumber;
        row.FirstName = data.FirstName;
        row.LastName = data.LastName;
        row.DateOfBirth = data.DateOfBirth;
        row.Phone = data.Phone;
        row.Email = data.Email;
        row.Address = data.Address;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicatePatientDocument(exception))
        {
            throw new PatientDocumentAlreadyExistsException();
        }
    }

    private static bool IsDuplicatePatientDocument(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_Patients_DocumentType_DocumentNumber"
        };
}
