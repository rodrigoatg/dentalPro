using DentalPro.Application.Patients;
using DentalPro.Application.AccessControl;
using DentalPro.Api.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalPro.Api.Patients;

public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/patients", CreateAsync).RequirePermission(PermissionCatalog.PatientsCreate);
        endpoints.MapGet("/api/patients", ListAsync).RequirePermission(PermissionCatalog.PatientsView);
        endpoints.MapGet("/api/patients/count", CountAsync).RequirePermission(PermissionCatalog.PatientsView);
        endpoints.MapGet("/api/patients/{id:guid}", GetAsync).RequirePermission(PermissionCatalog.PatientsView);
        endpoints.MapPut("/api/patients/{id:guid}", UpdateAsync).RequirePermission(PermissionCatalog.PatientsUpdate);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreatePatientRequest request,
        CreatePatient createPatient,
        CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request.DocumentType, request.DocumentNumber, request.FirstName,
            request.LastName, request.DateOfBirth, request.Email);
        if (validationErrors.Count > 0) return Results.ValidationProblem(validationErrors);

        try
        {
            var patient = await createPatient.ExecuteAsync(
                request.DocumentType!,
                request.DocumentNumber!,
                request.FirstName!,
                request.LastName!,
                request.DateOfBirth,
                request.Phone,
                request.Email,
                request.Address,
                cancellationToken);

            var response = new PatientResponse(
                patient.Id,
                patient.DocumentType,
                patient.DocumentNumber,
                patient.FirstName,
                patient.LastName,
                patient.DateOfBirth,
                patient.Phone,
                patient.Email,
                patient.Address,
                patient.IsActive,
                patient.CreatedAt,
                patient.MedicalRecord.MedicalRecordNumber
                    ?? throw new InvalidOperationException("Medical record number was not assigned."),
                patient.UpdatedAt);

            return Results.Json(response, statusCode: StatusCodes.Status201Created);
        }
        catch (PatientDocumentAlreadyExistsException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Patient document already exists.");
        }
        catch (ArgumentException exception)
        {
            var field = exception.ParamName ?? "request";
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [field] = [exception.Message]
            });
        }
    }

    private static async Task<IResult> ListAsync(
        string? search,
        ListPatients listPatients,
        CancellationToken cancellationToken)
    {
        var patients = await listPatients.ExecuteAsync(search, cancellationToken);
        var response = patients.Select(patient => new PatientListItemResponse(
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
            patient.MedicalRecordNumber));

        return Results.Ok(response);
    }

    private static async Task<IResult> CountAsync([FromServices] CountPatients countPatients, CancellationToken cancellationToken) =>
        Results.Ok(new PatientCountResponse(await countPatients.ExecuteAsync(cancellationToken)));

    private static async Task<IResult> GetAsync(Guid id, [FromServices] GetPatient getPatient, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await getPatient.ExecuteAsync(id, cancellationToken);
            return Results.Ok(new PatientResponse(patient.Id, patient.DocumentType, patient.DocumentNumber,
                patient.FirstName, patient.LastName, patient.DateOfBirth, patient.Phone, patient.Email,
                patient.Address, patient.IsActive, patient.CreatedAt, patient.MedicalRecordNumber, patient.UpdatedAt));
        }
        catch (PatientNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdatePatientRequest request, [FromServices] UpdatePatient updatePatient, CancellationToken cancellationToken)
    {
        var errors = Validate(request.DocumentType, request.DocumentNumber, request.FirstName,
            request.LastName, request.DateOfBirth, request.Email);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        try
        {
            await updatePatient.ExecuteAsync(id, new UpdatePatientData(request.DocumentType!, request.DocumentNumber!,
                request.FirstName!, request.LastName!, request.DateOfBirth, request.Phone, request.Email, request.Address),
                cancellationToken);
            return Results.NoContent();
        }
        catch (PatientNotFoundException)
        {
            return Results.NotFound();
        }
        catch (PatientDocumentAlreadyExistsException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Patient document already exists.");
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { [exception.ParamName ?? "request"] = [exception.Message] });
        }
    }

    private static Dictionary<string, string[]> Validate(
        string? documentType, string? documentNumber, string? firstName, string? lastName,
        DateOnly? dateOfBirth, string? email)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequired(errors, nameof(documentType), documentType);
        AddRequired(errors, nameof(documentNumber), documentNumber);
        AddRequired(errors, nameof(firstName), firstName);
        AddRequired(errors, nameof(lastName), lastName);
        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            errors[nameof(dateOfBirth)] = ["Date of birth cannot be in the future."];
        if (!string.IsNullOrWhiteSpace(email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            errors[nameof(email)] = ["A valid email address is required."];
        return errors;
    }

    private static void AddRequired(Dictionary<string, string[]> errors, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[name] = [$"{name} is required."];
    }
}
