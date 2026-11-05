using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymRatService.API.Filters;

public class ValidationActionFilter(IInputValidator inputValidator) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is not null)
            {
                await inputValidator.ValidateAsync(argument, context.HttpContext.RequestAborted);
            }
        }

        await next();
    }
}
