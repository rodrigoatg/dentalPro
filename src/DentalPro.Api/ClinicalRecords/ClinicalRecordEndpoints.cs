using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DentalPro.Api.Authorization;
using DentalPro.Application.AccessControl;
using DentalPro.Application.ClinicalRecords;
using DentalPro.Domain.Patients;
using DentalPro.Domain.Treatments;

namespace DentalPro.Api.ClinicalRecords;

public static class ClinicalRecordEndpoints
{
    public static IEndpointRouteBuilder MapClinicalRecordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/patients/{patientId:guid}/medical-record")
            .AddEndpointFilter<ClinicalRecordExceptionEndpointFilter>();
        group.MapGet("", GetAsync).RequirePermission(PermissionCatalog.ClinicalRecordsView);
        group.MapPost("/entries", CreateAsync).RequirePermission(PermissionCatalog.ClinicalEntriesCreate);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(Guid patientId, ClaimsPrincipal principal,
        GetClinicalRecord useCase, CancellationToken cancellationToken)
    {
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();
        var record = await useCase.ExecuteAsync(actorId, patientId, cancellationToken);
        return Results.Ok(new ClinicalRecordResponse(record.Id, record.PatientId, record.PatientFirstName,
            record.PatientLastName, record.MedicalRecordNumber,
            record.StartedAt, record.LastClinicalActivityAt, record.Entries.Select(entry => new ClinicalEntryResponse(
                entry.Id, entry.Type.ToString(), entry.DentistId, entry.DentistFirstName, entry.DentistLastName,
                entry.AppointmentId, entry.OccurredAt, entry.Diagnosis, entry.Notes,
                entry.Versions.Select(version => new ClinicalEntryVersionResponse(version.VersionNumber,
                    version.DentistId, version.AppointmentId, version.Type.ToString(), version.OccurredAt,
                    version.Diagnosis, version.Notes, version.TreatmentIds, version.CreatedAt, version.CreatedBy,
                    version.CreatedByFirstName, version.CreatedByLastName, version.ChangeReason)).ToArray(),
                entry.Treatments.Select(treatment => new ClinicalTreatmentResponse(treatment.Id, treatment.DentistId,
                    treatment.DentistFirstName, treatment.DentistLastName, treatment.Type, treatment.OccurredAt,
                    treatment.Description, treatment.Priority, treatment.Status, treatment.Cost,
                    treatment.Teeth.Select(tooth => new ClinicalTreatmentToothResponse(tooth.ToothNumber,
                        tooth.Status, tooth.CompletedAt, tooth.Notes, tooth.ResultingToothState)).ToArray())).ToArray())).ToArray()));
    }

    private static async Task<IResult> CreateAsync(Guid patientId, CreateClinicalEntryRequest request,
        ClaimsPrincipal principal, CreateClinicalEntry useCase, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null) return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["request"] = ["A request body is required."] });
        if (patientId == Guid.Empty) errors[nameof(patientId)] = ["A valid patientId is required."];
        if (string.IsNullOrWhiteSpace(request.Notes)) errors[nameof(request.Notes)] = ["Notes are required."];
        if (request.OccurredAt == default) errors[nameof(request.OccurredAt)] = ["OccurredAt is required."];
        if (request.Notes?.Length > 20_000) errors[nameof(request.Notes)] = ["Notes are too long."];
        if (request.Diagnosis?.Length > 2_000) errors[nameof(request.Diagnosis)] = ["Diagnosis is too long."];
        if (!Enum.TryParse<ClinicalEntryType>(request.Type, true, out var entryType) || !Enum.IsDefined(entryType))
            errors[nameof(request.Type)] = ["Type is invalid."];
        var treatmentCommands = new List<ClinicalTreatmentCommand>();
        foreach (var (treatment, index) in (request.Treatments ?? []).Select((value, index) => (value, index)))
        {
            if (treatment is null)
            {
                errors[$"treatments[{index}]"] = ["Treatment data is required."];
                continue;
            }
            if (!Enum.TryParse<TreatmentType>(treatment.Type, true, out var type) || !Enum.IsDefined(type))
                errors[$"treatments[{index}].type"] = ["Type is invalid."];
            if (!Enum.TryParse<TreatmentStatus>(treatment.Status, true, out var status) || !Enum.IsDefined(status))
                errors[$"treatments[{index}].status"] = ["Status is invalid for initial registration."];
            if (!Enum.TryParse<TreatmentPriority>(treatment.Priority, true, out var priority) || !Enum.IsDefined(priority))
                errors[$"treatments[{index}].priority"] = ["Priority is invalid."];
            if (string.IsNullOrWhiteSpace(treatment.Description))
                errors[$"treatments[{index}].description"] = ["Description is required."];
            if (treatment.Cost < 0) errors[$"treatments[{index}].cost"] = ["Cost cannot be negative."];
            try
            {
                treatmentCommands.Add(new ClinicalTreatmentCommand(type, treatment.Description, status,
                    (treatment.ToothNumbers ?? []).ToArray(), priority, treatment.Cost,
                    (treatment.CompletedToothNumbers ?? []).ToArray()));
            }
            catch (ArgumentException) { }
        }
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryActor(principal, out var actorId)) return Results.Unauthorized();

        var id = await useCase.ExecuteAsync(actorId, new CreateClinicalEntryCommand(patientId,
            request.AppointmentId, entryType, request.OccurredAt, request.Diagnosis, request.Notes!,
            treatmentCommands), cancellationToken);
        return Results.Json(new { id }, statusCode: StatusCodes.Status201Created);
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}
