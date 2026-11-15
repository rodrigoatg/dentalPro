using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentalPro.Api.Authorization;
using DentalPro.Application.AccessControl;
using DentalPro.Application.Appointments;
using DentalPro.Domain.Appointments;

namespace DentalPro.Api.Appointments;

public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var appointments = endpoints.MapGroup("/api/appointments")
            .AddEndpointFilter<AppointmentExceptionEndpointFilter>();

        appointments.MapGet("", ListAsync)
            .RequirePermission(PermissionCatalog.AppointmentsView);
        appointments.MapGet("/availability", GetAvailabilityAsync)
            .RequirePermission(PermissionCatalog.AppointmentsView);
        appointments.MapGet("/dentists", ListDentistsAsync)
            .RequirePermission(PermissionCatalog.AppointmentsCreate);
        appointments.MapPost("", CreateAsync)
            .RequirePermission(PermissionCatalog.AppointmentsCreate);
        appointments.MapPost("/{id}/reschedule", RescheduleAsync)
            .RequirePermission(PermissionCatalog.AppointmentsUpdate);
        appointments.MapPost("/{id}/cancel", CancelAsync)
            .RequirePermission(PermissionCatalog.AppointmentsCancel);
        appointments.MapPost("/{id}/confirm", ConfirmAsync)
            .RequirePermission(PermissionCatalog.AppointmentsConfirm);
        appointments.MapPost("/{id}/complete", CompleteAsync)
            .RequirePermission(PermissionCatalog.AppointmentsComplete);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? dentistId,
        ListAppointments listAppointments,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null) errors[nameof(from)] = ["From is required."];
        if (to is null) errors[nameof(to)] = ["To is required."];
        if (from.HasValue && to.HasValue && from >= to)
            errors[nameof(from)] = ["From must be earlier than To."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();

        var appointments = await listAppointments.ExecuteAsync(
            actorId, new AppointmentQuery(from!.Value, to!.Value, dentistId), cancellationToken);
        return Results.Ok(await MapAppointmentsAsync(appointments, participants, cancellationToken));
    }

    private static async Task<IResult> GetAvailabilityAsync(
        ClaimsPrincipal principal,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? dentistId,
        Guid? patientId,
        GetAppointmentAvailability getAvailability,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null) errors[nameof(from)] = ["From is required."];
        if (to is null) errors[nameof(to)] = ["To is required."];
        if (dentistId is null || dentistId == Guid.Empty)
            errors[nameof(dentistId)] = ["A valid dentistId is required."];
        if (patientId == Guid.Empty) errors[nameof(patientId)] = ["PatientId must be a non-empty GUID."];
        if (from.HasValue && to.HasValue && from >= to)
            errors[nameof(from)] = ["From must be earlier than To."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();

        var availability = await getAvailability.ExecuteAsync(actorId,
            new AppointmentAvailabilityQuery(from!.Value, to!.Value, dentistId!.Value, patientId), cancellationToken);
        return Results.Ok(new AppointmentAvailabilityResponse(
            availability.AvailableIntervals.Select(interval =>
                new AppointmentIntervalResponse(interval.Start, interval.End)).ToArray(),
            availability.DefaultAppointmentDurationMinutes,
            availability.StartIncrementMinutes));
    }

    private static async Task<IResult> ListDentistsAsync(
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var dentists = await participants.ListActiveDentistsAsync(cancellationToken);
        return Results.Ok(dentists.Select(dentist => new AppointmentDentistOptionResponse(
            dentist.Id, dentist.FirstName, dentist.LastName)));
    }

    private static async Task<IResult> CreateAsync(
        CreateAppointmentRequest request,
        ClaimsPrincipal principal,
        CreateAppointment createAppointment,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var errors = ValidateAppointmentInput(request.PatientId, request.DentistId,
            request.StartDateTime, request.DurationMinutes);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();

        var appointment = await createAppointment.ExecuteAsync(actorId,
            new CreateAppointmentCommand(request.PatientId, request.DentistId, request.StartDateTime,
                request.DurationMinutes, request.Notes), cancellationToken);
        var response = await MapAppointmentAsync(appointment, participants, cancellationToken);
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RescheduleAsync(
        Guid id,
        RescheduleAppointmentRequest request,
        ClaimsPrincipal principal,
        RescheduleAppointment rescheduleAppointment,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var errors = ValidateAppointmentInput(null, request.DentistId,
            request.StartDateTime, request.DurationMinutes);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();

        var appointment = await rescheduleAppointment.ExecuteAsync(actorId,
            new RescheduleAppointmentCommand(id, request.DentistId, request.StartDateTime,
                request.DurationMinutes, request.Notes), cancellationToken);
        return Results.Ok(await MapAppointmentAsync(appointment, participants, cancellationToken));
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        ClaimsPrincipal principal,
        CancelAppointment cancelAppointment,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();
        var appointment = await cancelAppointment.ExecuteAsync(actorId, id, cancellationToken);
        return Results.Ok(await MapAppointmentAsync(appointment, participants, cancellationToken));
    }

    private static async Task<IResult> ConfirmAsync(
        Guid id,
        ClaimsPrincipal principal,
        ConfirmAppointment confirmAppointment,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();
        var appointment = await confirmAppointment.ExecuteAsync(actorId, id, cancellationToken);
        return Results.Ok(await MapAppointmentAsync(appointment, participants, cancellationToken));
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        ClaimsPrincipal principal,
        CompleteAppointment completeAppointment,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(principal, out var actorId)) return Results.Unauthorized();
        var appointment = await completeAppointment.ExecuteAsync(actorId, id, cancellationToken);
        return Results.Ok(await MapAppointmentAsync(appointment, participants, cancellationToken));
    }

    private static Dictionary<string, string[]> ValidateAppointmentInput(
        Guid? patientId, Guid dentistId, DateTimeOffset startDateTime, int? durationMinutes)
    {
        var errors = new Dictionary<string, string[]>();
        if (patientId == Guid.Empty) errors[nameof(patientId)] = ["A valid patientId is required."];
        if (dentistId == Guid.Empty) errors[nameof(dentistId)] = ["A valid dentistId is required."];
        if (startDateTime == default) errors["startDateTime"] = ["StartDateTime is required."];
        if (durationMinutes is <= 0) errors["durationMinutes"] = ["DurationMinutes must be positive."];
        return errors;
    }

    private static async Task<IReadOnlyList<AppointmentResponse>> MapAppointmentsAsync(
        IReadOnlyList<Appointment> appointments,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        if (appointments.Count == 0) return [];
        var patientNames = await participants.GetPatientNamesAsync(
            appointments.Select(appointment => appointment.PatientId).Distinct().ToArray(), cancellationToken);
        var dentistNames = await participants.GetDentistNamesAsync(
            appointments.Select(appointment => appointment.DentistId).Distinct().ToArray(), cancellationToken);
        return appointments.Select(appointment => MapAppointment(appointment, patientNames, dentistNames)).ToArray();
    }

    private static async Task<AppointmentResponse> MapAppointmentAsync(
        Appointment appointment,
        IAppointmentParticipantQuery participants,
        CancellationToken cancellationToken)
    {
        var patientNames = await participants.GetPatientNamesAsync([appointment.PatientId], cancellationToken);
        var dentistNames = await participants.GetDentistNamesAsync([appointment.DentistId], cancellationToken);
        return MapAppointment(appointment, patientNames, dentistNames);
    }

    private static AppointmentResponse MapAppointment(
        Appointment appointment,
        IReadOnlyDictionary<Guid, AppointmentParticipantName> patientNames,
        IReadOnlyDictionary<Guid, AppointmentParticipantName> dentistNames)
    {
        if (!patientNames.TryGetValue(appointment.PatientId, out var patient))
            throw new InvalidOperationException("The appointment patient could not be loaded for the response.");
        if (!dentistNames.TryGetValue(appointment.DentistId, out var dentist))
            throw new InvalidOperationException("The appointment dentist could not be loaded for the response.");

        return new AppointmentResponse(
            appointment.Id, appointment.PatientId, patient.FirstName, patient.LastName,
            appointment.DentistId, dentist.FirstName, dentist.LastName,
            appointment.StartDateTime, appointment.EndDateTime, appointment.DurationMinutes,
            appointment.Status.ToString(), appointment.Notes, appointment.RescheduledFromAppointmentId,
            appointment.CreatedAt, appointment.UpdatedAt);
    }

    private static bool TryGetActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}
