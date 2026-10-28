using FluentValidation;
using GymRatService.Common.Errors;
using GymRatService.Service.Exceptions;
using Microsoft.Extensions.Localization;

namespace GymRatService.API.Middleware;

public class BusinessLogicExceptionMiddleware(
    RequestDelegate next,
    IStringLocalizer<BusinessLogicExceptionMiddleware> localizer)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BusinessLogicException exception)
        {
            await WriteBadRequestAsync(context, new ErrorResponse(exception.Code, localizer[exception.Code, [.. exception.Arguments]]));
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .Select(error => new ValidationError(error.PropertyName, error.ErrorMessage))
                .ToList();

            await WriteBadRequestAsync(context, new ErrorResponse(ErrorCodes.InvalidRequest, localizer[ErrorCodes.InvalidRequest], errors));
        }
    }

    private static async Task WriteBadRequestAsync(HttpContext context, ErrorResponse response)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        await context.Response.WriteAsJsonAsync(response);
    }
}
