using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Application.ClinicalRecords;
using DentalPro.Domain.Patients;
using DentalPro.Domain.Treatments;

namespace DentalPro.UnitTests;

public sealed class ClinicalRecordApplicationTests
{
    [Fact]
    public async Task CreateEntry_without_permission_does_not_query_or_write_record()
    {
        var repository = new FakeRepository();
        var participants = new FakeParticipants();
        var useCase = CreateUseCase(repository, participants, new FakePermissions(), new FakeAudit());

        await Assert.ThrowsAsync<ClinicalRecordOutOfScopeException>(() => useCase.ExecuteAsync(Guid.NewGuid(),
            Command(Guid.NewGuid())));

        Assert.Equal(0, participants.PatientStatusCalls);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public async Task CreateEntry_with_scope_persists_entry_treatments_and_audit_events()
    {
        var dentistId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var repository = new FakeRepository { InScope = true, MedicalRecord = CreateMedicalRecord(patientId) };
        var audit = new FakeAudit();
        var permissions = new FakePermissions(PermissionCatalog.ClinicalEntriesCreate, PermissionCatalog.TreatmentsCreate);
        var useCase = CreateUseCase(repository, new FakeParticipants(), permissions, audit);

        var entryId = await useCase.ExecuteAsync(dentistId, Command(patientId));

        Assert.NotEqual(Guid.Empty, entryId);
        Assert.Equal(entryId, repository.Entry?.Id);
        Assert.Equal(2, repository.Treatments.Count);
        Assert.Equal(2, repository.Entry!.TreatmentIds.Count);
        Assert.Equal(2, repository.Treatments[0].Teeth.Count);
        Assert.Equal(TreatmentStatus.PartiallyCompleted, repository.Treatments[1].Status);
        Assert.Equal(3, audit.Events.Count);
    }

    [Fact]
    public async Task CreateEntry_rejects_partial_treatment_without_both_completed_and_pending_teeth()
    {
        var patientId = Guid.NewGuid();
        var repository = new FakeRepository { InScope = true, MedicalRecord = CreateMedicalRecord(patientId) };
        var permissions = new FakePermissions(PermissionCatalog.ClinicalEntriesCreate, PermissionCatalog.TreatmentsCreate);
        var useCase = CreateUseCase(repository, new FakeParticipants(), permissions, new FakeAudit());
        var invalid = Command(patientId) with
        {
            Treatments = [new(TreatmentType.Restoration, "Restauración parcial",
                TreatmentStatus.PartiallyCompleted, [21, 22])]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(Guid.NewGuid(), invalid));
        Assert.Equal(0, repository.AddCalls);
    }

    private static CreateClinicalEntry CreateUseCase(FakeRepository repository, FakeParticipants participants,
        FakePermissions permissions, FakeAudit audit) => new(repository, participants, permissions,
        new InlineUnitOfWork(), audit);

    private static CreateClinicalEntryCommand Command(Guid patientId) => new(patientId, null,
        ClinicalEntryType.Consultation, DateTimeOffset.UtcNow, "Control", "Se revisan dos piezas.",
        [new(TreatmentType.Restoration, "Restauración", TreatmentStatus.Planned, [11, 12]),
         new(TreatmentType.Restoration, "Restauración parcial", TreatmentStatus.PartiallyCompleted,
             [21, 22], CompletedToothNumbers: [21])]);

    private static MedicalRecord CreateMedicalRecord(Guid patientId)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return MedicalRecord.Rehydrate(id, patientId, 17, now, null, now, now,
            Odontogram.Rehydrate(Guid.NewGuid(), id, now, now));
    }

    private sealed class FakePermissions(params string[] codes) : IEffectivePermissionQuery
    {
        public Task<IReadOnlySet<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(codes.ToHashSet(StringComparer.Ordinal));
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(codes.Contains(permissionCode, StringComparer.Ordinal));
    }

    private sealed class FakeParticipants : IAppointmentParticipantQuery
    {
        public int PatientStatusCalls { get; private set; }
        public Task<AppointmentPatientStatus> GetPatientStatusAsync(Guid patientId, CancellationToken cancellationToken = default)
        { PatientStatusCalls++; return Task.FromResult(AppointmentPatientStatus.Active); }
        public Task<AppointmentDentistStatus> GetDentistStatusAsync(Guid dentistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AppointmentDentistStatus.ActiveDentist);
        public Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetPatientNamesAsync(IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AppointmentParticipantName>>(new Dictionary<Guid, AppointmentParticipantName>());
        public Task<IReadOnlyDictionary<Guid, AppointmentParticipantName>> GetDentistNamesAsync(IReadOnlyCollection<Guid> dentistIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AppointmentParticipantName>>(new Dictionary<Guid, AppointmentParticipantName>());
        public Task<IReadOnlyList<AppointmentDentistOption>> ListActiveDentistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppointmentDentistOption>>([]);
    }

    private sealed class FakeRepository : IClinicalRecordRepository
    {
        public bool InScope { get; init; }
        public MedicalRecord? MedicalRecord { get; init; }
        public ClinicalEntry? Entry { get; private set; }
        public IReadOnlyList<Treatment> Treatments { get; private set; } = [];
        public int AddCalls { get; private set; }
        public Task<MedicalRecord?> GetForUpdateAsync(Guid patientId, CancellationToken cancellationToken = default) => Task.FromResult(MedicalRecord);
        public Task<ClinicalRecordDetails?> GetAsync(Guid patientId, CancellationToken cancellationToken = default) => Task.FromResult<ClinicalRecordDetails?>(null);
        public Task<bool> HasPatientScopeAsync(Guid patientId, Guid dentistId, CancellationToken cancellationToken = default) => Task.FromResult(InScope);
        public Task<bool> AppointmentBelongsToAsync(Guid appointmentId, Guid patientId, Guid dentistId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task UpdateMedicalRecordAsync(MedicalRecord medicalRecord, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddEntryAsync(ClinicalEntry entry, ClinicalEntryVersion initialVersion, IReadOnlyList<Treatment> treatments, CancellationToken cancellationToken = default)
        { Entry = entry; Treatments = treatments; AddCalls++; return Task.CompletedTask; }
    }

    private sealed class InlineUnitOfWork : IClinicalUnitOfWork
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class FakeAudit : IClinicalAuditWriter
    {
        public List<ClinicalAuditEntry> Events { get; } = [];
        public Task AddAsync(ClinicalAuditEntry entry, CancellationToken cancellationToken = default)
        { Events.Add(entry); return Task.CompletedTask; }
    }
}
