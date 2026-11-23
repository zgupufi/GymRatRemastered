using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Filters;

public sealed class AllowedOriginFilter(IOptions<CorsSettings> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var origin = context.HttpContext.Request.Headers.Origin.ToString();

        if (origin.Length > 0 && !options.Value.SameOrigin && !IsAllowed(origin))
        {
            throw new BusinessLogicException(ErrorCodes.OriginNotAllowed);
        }

        await next();
    }

    private bool IsAllowed(string origin)
    {
        return options.Value.AllowedOrigins.Any(allowed => string.Equals(allowed.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }
}
