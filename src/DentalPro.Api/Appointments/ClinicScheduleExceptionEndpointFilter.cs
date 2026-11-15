using DentalPro.Application.Appointments;

namespace DentalPro.Api.Appointments;

public sealed class ClinicScheduleExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ClinicScheduleNotConfiguredException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Clinic schedule is not configured.");
        }
        catch (ClinicScheduleAlreadyConfiguredException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Clinic schedule is already configured.");
        }
        catch (ClinicScheduleConfigurationException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["schedule"] = ["The clinic schedule configuration is invalid."] });
        }
        catch (AppointmentOutOfScopeException)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Clinic schedule access denied.");
        }
        catch (ArgumentException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["schedule"] = ["The clinic schedule data is invalid."] });
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ClinicScheduleExceptionEndpointFilter>>();
            logger.LogError(exception, "Unexpected error while processing a clinic schedule endpoint.");
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected server error.");
        }
    }
}
