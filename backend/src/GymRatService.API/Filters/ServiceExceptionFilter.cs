using GymRatService.Common.Errors;
using GymRatService.Service.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymRatService.API.Filters;

/// <summary>Turns business errors from the service layer into 400 responses that name the error.</summary>
public class ServiceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ServiceException exception)
        {
            return;
        }

        context.Result = new BadRequestObjectResult(new ErrorResponse(exception.Code, exception.Message));
        context.ExceptionHandled = true;
    }
}
