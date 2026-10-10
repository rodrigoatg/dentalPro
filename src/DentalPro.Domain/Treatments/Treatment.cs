namespace DentalPro.Domain.Treatments;

public readonly record struct ToothNumber
{
    private ToothNumber(int value) => Value = value;
    public int Value { get; }
    public static ToothNumber Create(int value)
    {
        if (value / 10 is < 1 or > 4 || value % 10 is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(value), "Invalid permanent dentition FDI tooth number.");
        return new(value);
    }
}
public enum TreatmentStatus { Planned, InProgress, PartiallyCompleted, Completed, Cancelled }
public enum TreatmentToothStatus { Planned, InProgress, Completed, Cancelled }
public enum TreatmentPriority { Low, Medium, High, Urgent }
public enum TreatmentType { Restoration, Extraction, RootCanal, Cleaning, Crown, Other }

public sealed class TreatmentTooth
{
    internal TreatmentTooth(ToothNumber number) { ToothNumber = number; Status = TreatmentToothStatus.Planned; }
    public ToothNumber ToothNumber { get; }
    public TreatmentToothStatus Status { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Notes { get; private set; }
    public string? ResultingToothState { get; private set; }
    internal void Start() { Ensure(TreatmentToothStatus.Planned); Status = TreatmentToothStatus.InProgress; }
    internal void Complete(DateTimeOffset at, string? state, string? notes)
    {
        Ensure(TreatmentToothStatus.Planned, TreatmentToothStatus.InProgress);
        Status = TreatmentToothStatus.Completed; CompletedAt = at; ResultingToothState = state; Notes = notes ?? Notes;
    }
    internal void Cancel() { Ensure(TreatmentToothStatus.Planned, TreatmentToothStatus.InProgress); Status = TreatmentToothStatus.Cancelled; }
    private void Ensure(params TreatmentToothStatus[] allowed)
    { if (!allowed.Contains(Status)) throw new InvalidOperationException($"Cannot change tooth in status {Status}."); }
}

public sealed class Treatment
{
    private readonly List<TreatmentTooth> _teeth = [];
    private Treatment(Guid recordId, Guid dentistId, TreatmentType type, DateTimeOffset occurredAt, string description, TreatmentPriority priority, decimal? cost)
    {
        Id = Guid.NewGuid(); MedicalRecordId = recordId; DentistId = dentistId; TreatmentType = type;
        OccurredAt = occurredAt; Description = description; Priority = priority; Cost = cost;
        Status = TreatmentStatus.Planned; RecordedAt = CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }
    public Guid Id { get; }
    public Guid MedicalRecordId { get; }
    public Guid DentistId { get; }
    public TreatmentType TreatmentType { get; }
    public DateTimeOffset OccurredAt { get; }
    public DateTimeOffset RecordedAt { get; }
    public string Description { get; }
    public TreatmentPriority Priority { get; }
    public TreatmentStatus Status { get; private set; }
    public bool IsFinalized { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public decimal? Cost { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<TreatmentTooth> Teeth => _teeth.AsReadOnly();
    public static Treatment Create(Guid medicalRecordId, Guid dentistId, TreatmentType treatmentType, DateTimeOffset occurredAt,
        string description, IEnumerable<ToothNumber>? teeth = null, TreatmentPriority priority = TreatmentPriority.Medium, decimal? cost = null)
    {
        if (medicalRecordId == Guid.Empty) throw new ArgumentException("Medical record is required.", nameof(medicalRecordId));
        if (dentistId == Guid.Empty) throw new ArgumentException("Dentist is required.", nameof(dentistId));
        if (!Enum.IsDefined(treatmentType)) throw new ArgumentOutOfRangeException(nameof(treatmentType));
        if (!Enum.IsDefined(priority)) throw new ArgumentOutOfRangeException(nameof(priority));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        var treatment = new Treatment(medicalRecordId, dentistId, treatmentType, occurredAt, description, priority, cost);
        if (teeth is not null)
        {
            var values = teeth.ToArray();
            if (values.Distinct().Count() != values.Length) throw new ArgumentException("Duplicate tooth.", nameof(teeth));
            treatment._teeth.AddRange(values.Select(x => new TreatmentTooth(x)));
        }
        return treatment;
    }
    public void StartTooth(ToothNumber number) { EnsureProgressable(); Find(number).Start(); Refresh(); }
    public void CompleteTooth(ToothNumber number, string? resultingToothState = null, string? notes = null, DateTimeOffset? completedAt = null)
    { EnsureProgressable(); Find(number).Complete(completedAt ?? DateTimeOffset.UtcNow, resultingToothState, notes); Refresh(); }
    public void CancelTooth(ToothNumber number) { EnsureProgressable(); Find(number).Cancel(); Refresh(); }
    public void FinalizeTreatment()
    {
        if (IsFinalized) throw new InvalidOperationException("Treatment is already finalized.");
        IsFinalized = true;
        FinalizedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }
    private TreatmentTooth Find(ToothNumber n) => _teeth.SingleOrDefault(x => x.ToothNumber == n) ?? throw new ArgumentException("Tooth is not part of this treatment.", nameof(n));
    private void EnsureProgressable()
    {
        if (IsFinalized || Status is TreatmentStatus.Completed or TreatmentStatus.Cancelled)
            throw new InvalidOperationException("Treatment is closed.");
    }
    private void Refresh()
    {
        var completed = _teeth.Count(x => x.Status == TreatmentToothStatus.Completed);
        var active = _teeth.Count(x => x.Status is TreatmentToothStatus.Planned or TreatmentToothStatus.InProgress);
        Status = completed == _teeth.Count && _teeth.Count > 0 ? TreatmentStatus.Completed : completed > 0 ? TreatmentStatus.PartiallyCompleted :
            active == 0 ? TreatmentStatus.Planned : _teeth.Any(x => x.Status == TreatmentToothStatus.InProgress) ? TreatmentStatus.InProgress : TreatmentStatus.Planned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
