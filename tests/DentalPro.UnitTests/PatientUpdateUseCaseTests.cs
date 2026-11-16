using DentalPro.Application.Patients;

namespace DentalPro.UnitTests;

public sealed class PatientUpdateUseCaseTests
{
    [Fact]
    public async Task UpdatePatient_ValidatesAndNormalizesPersonalFields()
    {
        var repository = new RecordingPatientRepository();
        var useCase = new UpdatePatient(repository);

        await useCase.ExecuteAsync(Guid.NewGuid(), new UpdatePatientData(
            " DNI ", " 123 ", " Ana ", " Pérez ", null, " 555 ", " ana@example.com ", " Calle 1 "));

        Assert.Equal("DNI", repository.Data!.DocumentType);
        Assert.Equal("123", repository.Data.DocumentNumber);
        Assert.Equal("Ana", repository.Data.FirstName);
        Assert.Equal("555", repository.Data.Phone);
        Assert.Equal("ana@example.com", repository.Data.Email);
        Assert.Equal("Calle 1", repository.Data.Address);
    }

    [Fact]
    public async Task UpdatePatient_InvalidRequiredFieldsDoesNotPersist()
    {
        var repository = new RecordingPatientRepository();
        var useCase = new UpdatePatient(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(Guid.NewGuid(),
            new UpdatePatientData("DNI", "123", " ", "Pérez", null, null, null, null)));

        Assert.Null(repository.Data);
    }

    private sealed class RecordingPatientRepository : IPatientRepository
    {
        public UpdatePatientData? Data { get; private set; }
        public Task AddAsync(DentalPro.Domain.Patients.Patient patient, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PatientListItem>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PatientListItem>>([]);
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<PatientListItem>> SearchAsync(string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PatientListItem>>([]);
        public Task<PatientDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PatientDetails?>(null);
        public Task UpdateAsync(Guid id, UpdatePatientData data, CancellationToken cancellationToken = default) { Data = data; return Task.CompletedTask; }
    }
}
