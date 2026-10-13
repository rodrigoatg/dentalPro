using DentalPro.Application.Patients;
using DentalPro.Domain.Patients;

namespace DentalPro.UnitTests;

public sealed class PatientUseCaseTests
{
    [Fact]
    public async Task CreatePatient_CreatesAggregateAndPersistsThroughRepository()
    {
        var repository = new InMemoryPatientRepository();
        var useCase = new CreatePatient(repository);

        var patient = await useCase.ExecuteAsync("DNI", "123", "Ana", "Pérez");

        Assert.Same(patient, repository.AddedPatient);
        Assert.Equal(1, patient.MedicalRecord.MedicalRecordNumber);
        Assert.NotEqual(Guid.Empty, patient.MedicalRecord.Odontogram.Id);
    }

    [Fact]
    public async Task CreatePatient_PropagatesDomainValidationFailureWithoutPersisting()
    {
        var repository = new InMemoryPatientRepository();
        var useCase = new CreatePatient(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync("DNI", "123", " ", "Pérez"));

        Assert.Null(repository.AddedPatient);
    }

    [Fact]
    public async Task ListPatients_ReturnsRepositoryProjection()
    {
        var expected = new PatientListItem(
            Guid.NewGuid(), "DNI", "123", "Ana", "Pérez", null, null, null, true,
            DateTimeOffset.UtcNow, 17);
        var repository = new InMemoryPatientRepository { ListedPatients = [expected] };
        var useCase = new ListPatients(repository);

        var result = await useCase.ExecuteAsync();

        Assert.Equal([expected], result);
    }

    private sealed class InMemoryPatientRepository : IPatientRepository
    {
        public Patient? AddedPatient { get; private set; }
        public IReadOnlyList<PatientListItem> ListedPatients { get; init; } = [];

        public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
        {
            AddedPatient = patient;
            patient.MedicalRecord.AssignMedicalRecordNumber(1);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PatientListItem>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ListedPatients);
    }
}
