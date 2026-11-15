using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentalPro.Api.Authorization;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;

namespace DentalPro.Api.Appointments;

public static class ClinicScheduleEndpoints
{
    public static IEndpointRouteBuilder MapClinicScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var schedule = endpoints.MapGroup("/api/appointments/schedule")
            .AddEndpointFilter<ClinicScheduleExceptionEndpointFilter>();

        schedule.MapGet("", GetAsync).RequirePermission(PermissionCatalog.AppointmentsScheduleManage);
        schedule.MapGet("/settings", GetSettingsAsync).RequirePermission(PermissionCatalog.AppointmentsView);
        schedule.MapPost("", CreateAsync).RequirePermission(PermissionCatalog.AppointmentsScheduleManage);
        schedule.MapPut("", UpdateAsync).RequirePermission(PermissionCatalog.AppointmentsScheduleManage);
        schedule.MapPut("/exceptions/{date}", SetExceptionAsync).RequirePermission(PermissionCatalog.AppointmentsScheduleManage);
        schedule.MapDelete("/exceptions/{date}", RemoveExceptionAsync).RequirePermission(PermissionCatalog.AppointmentsScheduleManage);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, GetClinicSchedule useCase, CancellationToken ct)
    {
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();
        return Results.Ok(Map(await useCase.ExecuteAsync(actorId, ct)));
    }

    private static async Task<IResult> GetSettingsAsync(ClaimsPrincipal principal, IClinicScheduleQuery query, CancellationToken ct)
    {
        if (!TryActor(principal, out _)) return Results.Unauthorized();
        var settings = await query.GetSettingsAsync(ct);
        return Results.Ok(new ClinicScheduleSettingsResponse(settings.TimeZoneId,
            settings.DefaultAppointmentDurationMinutes, settings.StartIncrementMinutes));
    }

    private static async Task<IResult> CreateAsync(ConfigureClinicScheduleRequest request, ClaimsPrincipal principal,
        CreateClinicSchedule useCase, CancellationToken ct)
    {
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();
        if (!TryCommand(request, out var command, out var errors)) return Results.ValidationProblem(errors);
        return Results.Created("/api/appointments/schedule", Map(await useCase.ExecuteAsync(actorId, command!, ct)));
    }

    private static async Task<IResult> UpdateAsync(ConfigureClinicScheduleRequest request, ClaimsPrincipal principal,
        UpdateClinicSchedule useCase, CancellationToken ct)
    {
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();
        if (!TryCommand(request, out var command, out var errors)) return Results.ValidationProblem(errors);
        return Results.Ok(Map(await useCase.ExecuteAsync(actorId, command!, ct)));
    }

    private static async Task<IResult> SetExceptionAsync(string date, ClinicDateExceptionRequest request,
        ClaimsPrincipal principal, SetClinicScheduleException useCase, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed) || parsed != request.Date)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use a matching date in yyyy-MM-dd format."] });
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();
        var intervals = request.OpeningIntervals.Select(interval => (interval.Start, interval.End)).ToArray();
        return Results.Ok(Map(await useCase.ExecuteAsync(actorId, new SetClinicScheduleExceptionCommand(parsed, intervals), ct)));
    }

    private static async Task<IResult> RemoveExceptionAsync(string date, ClaimsPrincipal principal,
        RemoveClinicScheduleException useCase, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use yyyy-MM-dd format."] });
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();
        return Results.Ok(Map(await useCase.ExecuteAsync(actorId, parsed, ct)));
    }

    private static bool TryCommand(ConfigureClinicScheduleRequest request,
        out ConfigureClinicScheduleCommand? command, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var intervals = new List<WeeklyOpeningInterval>();
        foreach (var interval in request.WeeklyIntervals ?? [])
        {
            if (!Enum.TryParse<DayOfWeek>(interval.DayOfWeek, true, out var day) || !Enum.IsDefined(day))
            {
                errors[nameof(request.WeeklyIntervals)] = ["Each interval needs a valid day of week."];
                continue;
            }
            intervals.Add(new WeeklyOpeningInterval(day, interval.Start, interval.End));
        }
        if (errors.Count > 0)
        {
            command = null;
            return false;
        }
        command = new ConfigureClinicScheduleCommand(request.TimeZoneId, request.DefaultAppointmentDurationMinutes,
            request.StartIncrementMinutes, intervals);
        return true;
    }

    private static ClinicScheduleResponse Map(ClinicScheduleDetails schedule) => new(
        schedule.Id, schedule.TimeZoneId, schedule.DefaultAppointmentDurationMinutes, schedule.StartIncrementMinutes,
        schedule.WeeklyIntervals.Select(x => new WeeklyOpeningIntervalResponse(x.DayOfWeek.ToString(), x.Start, x.End)).ToArray(),
        schedule.DateExceptions.Select(x => new ClinicDateExceptionResponse(x.Date,
            x.Intervals.Select(i => new ClinicOpeningIntervalResponse(i.Start, i.End)).ToArray())).ToArray());

    private static bool TryActor(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}
