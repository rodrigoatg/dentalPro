using DentalPro.Domain.Appointments;
using DentalPro.Application.AccessControl;

namespace DentalPro.Application.Appointments;

public sealed record ClinicScheduleDetails(
    Guid Id,
    string TimeZoneId,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes,
    IReadOnlyList<WeeklyOpeningInterval> WeeklyIntervals,
    IReadOnlyList<ClinicDateException> DateExceptions);

public sealed record ConfigureClinicScheduleCommand(
    string TimeZoneId,
    int DefaultAppointmentDurationMinutes,
    int StartIncrementMinutes,
    IReadOnlyList<WeeklyOpeningInterval> WeeklyIntervals);

public sealed class GetClinicSchedule(IClinicScheduleRepository repository, IEffectivePermissionQuery permissions)
{
    public async Task<ClinicScheduleDetails> ExecuteAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await CreateClinicSchedule.EnsureSchedulePermissionAsync(permissions, actorId, cancellationToken);
        return ToDetails(await repository.GetAsync(cancellationToken)
            ?? throw new ClinicScheduleNotConfiguredException());
    }

    internal static ClinicScheduleDetails ToDetails(ClinicSchedule schedule) => new(
        schedule.Id, schedule.TimeZoneId, schedule.DefaultDurationMinutes, schedule.StartIncrementMinutes,
        schedule.WeeklyIntervals, schedule.DateExceptions);
}

public sealed class CreateClinicSchedule(
    IClinicScheduleRepository repository,
    IAppointmentUnitOfWork unitOfWork,
    IEffectivePermissionQuery permissions)
{
    public Task<ClinicScheduleDetails> ExecuteAsync(Guid actorId, ConfigureClinicScheduleCommand command,
        CancellationToken cancellationToken = default) => unitOfWork.ExecuteAsync(async ct =>
    {
        await EnsureSchedulePermissionAsync(permissions, actorId, ct);
        ArgumentNullException.ThrowIfNull(command);
        if (await repository.GetForUpdateAsync(ct) is not null) throw new ClinicScheduleAlreadyConfiguredException();
        ValidateTimeZone(command.TimeZoneId);
        var schedule = ClinicSchedule.Create(command.TimeZoneId, command.DefaultAppointmentDurationMinutes,
            command.StartIncrementMinutes, command.WeeklyIntervals);
        repository.Add(schedule);
        return GetClinicSchedule.ToDetails(schedule);
    }, cancellationToken);

    internal static void ValidateTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ClinicScheduleConfigurationException("The clinic time zone must be a valid IANA time-zone identifier.");
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _))
                throw new ClinicScheduleConfigurationException("The clinic time zone must be a valid IANA time-zone identifier.");
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ClinicScheduleConfigurationException("The clinic time zone must be a valid IANA time-zone identifier.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ClinicScheduleConfigurationException("The clinic time zone must be a valid IANA time-zone identifier.", exception);
        }
    }

    internal static async Task EnsureSchedulePermissionAsync(
        IEffectivePermissionQuery permissions, Guid actorId, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || !await permissions.HasPermissionAsync(
                actorId, AppointmentPermissionCodes.ScheduleManage, cancellationToken))
            throw new AppointmentOutOfScopeException();
    }
}

public sealed class UpdateClinicSchedule(
    IClinicScheduleRepository repository,
    IAppointmentUnitOfWork unitOfWork,
    IEffectivePermissionQuery permissions)
{
    public Task<ClinicScheduleDetails> ExecuteAsync(Guid actorId, ConfigureClinicScheduleCommand command,
        CancellationToken cancellationToken = default) => unitOfWork.ExecuteAsync(async ct =>
    {
        await CreateClinicSchedule.EnsureSchedulePermissionAsync(permissions, actorId, ct);
        ArgumentNullException.ThrowIfNull(command);
        var existing = await repository.GetForUpdateAsync(ct) ?? throw new ClinicScheduleNotConfiguredException();
        CreateClinicSchedule.ValidateTimeZone(command.TimeZoneId);
        var updated = existing.Update(command.TimeZoneId, command.DefaultAppointmentDurationMinutes,
            command.StartIncrementMinutes, command.WeeklyIntervals, existing.DateExceptions);
        await repository.UpdateAsync(updated, ct);
        return GetClinicSchedule.ToDetails(updated);
    }, cancellationToken);
}

public sealed record SetClinicScheduleExceptionCommand(
    DateOnly Date,
    IReadOnlyList<(TimeOnly Start, TimeOnly End)> OpeningIntervals);

public sealed class SetClinicScheduleException(
    IClinicScheduleRepository repository,
    IAppointmentUnitOfWork unitOfWork,
    IEffectivePermissionQuery permissions)
{
    public Task<ClinicScheduleDetails> ExecuteAsync(Guid actorId, SetClinicScheduleExceptionCommand command,
        CancellationToken cancellationToken = default) => unitOfWork.ExecuteAsync(async ct =>
    {
        await CreateClinicSchedule.EnsureSchedulePermissionAsync(permissions, actorId, ct);
        ArgumentNullException.ThrowIfNull(command);
        var existing = await repository.GetForUpdateAsync(ct) ?? throw new ClinicScheduleNotConfiguredException();
        var exception = new ClinicDateException(command.Date, command.OpeningIntervals);
        var exceptions = existing.DateExceptions.Where(item => item.Date != command.Date).Append(exception);
        var updated = existing.Update(existing.TimeZoneId, existing.DefaultDurationMinutes,
            existing.StartIncrementMinutes, existing.WeeklyIntervals, exceptions);
        await repository.UpdateAsync(updated, ct);
        return GetClinicSchedule.ToDetails(updated);
    }, cancellationToken);
}

public sealed class RemoveClinicScheduleException(
    IClinicScheduleRepository repository,
    IAppointmentUnitOfWork unitOfWork,
    IEffectivePermissionQuery permissions)
{
    public Task<ClinicScheduleDetails> ExecuteAsync(Guid actorId, DateOnly date,
        CancellationToken cancellationToken = default) => unitOfWork.ExecuteAsync(async ct =>
    {
        await CreateClinicSchedule.EnsureSchedulePermissionAsync(permissions, actorId, ct);
        var existing = await repository.GetForUpdateAsync(ct) ?? throw new ClinicScheduleNotConfiguredException();
        var updated = existing.Update(existing.TimeZoneId, existing.DefaultDurationMinutes,
            existing.StartIncrementMinutes, existing.WeeklyIntervals,
            existing.DateExceptions.Where(item => item.Date != date));
        await repository.UpdateAsync(updated, ct);
        return GetClinicSchedule.ToDetails(updated);
    }, cancellationToken);
}
