using DentalPro.Application.Appointments;

namespace DentalPro.Api.Appointments;

public sealed class AppointmentExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AppointmentNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Appointment not found.");
        }
        catch (AppointmentPatientUnavailableException exception)
        {
            return exception.Status == AppointmentPatientStatus.NotFound
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Patient not found.")
                : Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["patientId"] = ["The selected patient is not active."]
                });
        }
        catch (AppointmentDentistUnavailableException exception)
        {
            return exception.Status == AppointmentDentistStatus.NotFound
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Dentist not found.")
                : Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dentistId"] = ["The selected user is not an active dentist."]
                });
        }
        catch (AppointmentOutOfScopeException)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Appointment access denied.");
        }
        catch (AppointmentAvailabilityConflictException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Appointment availability conflict.");
        }
        catch (AppointmentOutsideWorkingHoursException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Appointment is outside clinic hours.");
        }
        catch (AppointmentInvalidStateTransitionException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Appointment state conflict.");
        }
        catch (ClinicScheduleNotConfiguredException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Clinic schedule is not configured.");
        }
        catch (AppointmentQueryValidationException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["The appointment query is invalid."]
            });
        }
        catch (AppointmentDurationInvalidException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["durationMinutes"] = ["Duration must be a positive number of minutes."]
            });
        }
        catch (AppointmentLocalTimeInvalidException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["startDateTime"] = ["The local time does not exist in the clinic time zone."]
            });
        }
        catch (AppointmentLocalTimeAmbiguousException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["startDateTime"] = ["The local time is ambiguous in the clinic time zone."]
            });
        }
        catch (AppointmentLocalTimeOffsetMismatchException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["startDateTime"] = ["The supplied offset does not match the clinic time zone."]
            });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ToCamelCase(exception.ParamName ?? "request")] = ["The supplied appointment data is invalid."]
            });
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<AppointmentExceptionEndpointFilter>>();
            logger.LogError(exception, "Unexpected error while processing an appointment endpoint.");
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected server error.");
        }
    }

    private static string ToCamelCase(string value) =>
        value.Length == 0 ? "request" : char.ToLowerInvariant(value[0]) + value[1..];
}
