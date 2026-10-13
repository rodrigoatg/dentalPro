using DentalPro.Application.Patients;
using Microsoft.AspNetCore.Mvc;

namespace DentalPro.Api.Patients;

public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/patients", CreateAsync);
        endpoints.MapGet("/api/patients", ListAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreatePatientRequest request,
        CreatePatient createPatient,
        CancellationToken cancellationToken)
    {
        if (request.DocumentType is null || request.DocumentNumber is null)
        {
            var errors = new Dictionary<string, string[]>();
            if (request.DocumentType is null)
            {
                errors[nameof(request.DocumentType)] = ["Document type is required."];
            }

            if (request.DocumentNumber is null)
            {
                errors[nameof(request.DocumentNumber)] = ["Document number is required."];
            }

            return Results.ValidationProblem(errors);
        }

        try
        {
            var patient = await createPatient.ExecuteAsync(
                request.DocumentType,
                request.DocumentNumber,
                request.FirstName,
                request.LastName,
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
                    ?? throw new InvalidOperationException("Medical record number was not assigned."));

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
        ListPatients listPatients,
        CancellationToken cancellationToken)
    {
        var patients = await listPatients.ExecuteAsync(cancellationToken);
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
}
