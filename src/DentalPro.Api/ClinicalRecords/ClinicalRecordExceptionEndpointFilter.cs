using DentalPro.Application.ClinicalRecords;

namespace DentalPro.Api.ClinicalRecords;

public sealed class ClinicalRecordExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (ClinicalRecordNotFoundException)
        { return Results.Problem(statusCode: 404, title: "Clinical record not found."); }
        catch (ClinicalPatientUnavailableException)
        { return Results.Problem(statusCode: 400, title: "Clinical information cannot be added to an inactive patient."); }
        catch (ClinicalRecordOutOfScopeException)
        { return Results.Problem(statusCode: 403, title: "Clinical record access denied."); }
        catch (ClinicalTreatmentPermissionException)
        { return Results.Problem(statusCode: 403, title: "Treatment creation is not permitted."); }
        catch (ClinicalDentistUnavailableException)
        { return Results.Problem(statusCode: 400, title: "The responsible dentist is unavailable."); }
        catch (ClinicalAppointmentMismatchException)
        { return Results.Problem(statusCode: 409, title: "Appointment does not match this patient and dentist."); }
        catch (ArgumentException exception)
        {
            var field = string.IsNullOrWhiteSpace(exception.ParamName) ? "request" :
                char.ToLowerInvariant(exception.ParamName[0]) + exception.ParamName[1..];
            return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = ["The supplied clinical data is invalid."] });
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ClinicalRecordExceptionEndpointFilter>>();
            logger.LogError(exception, "Unexpected error while processing a clinical record endpoint.");
            return Results.Problem(statusCode: 500, title: "Unexpected server error.");
        }
    }
}
