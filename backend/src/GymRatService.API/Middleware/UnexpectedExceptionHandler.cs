using GymRatService.Common.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Localization;

namespace GymRatService.API.Middleware;

internal static partial class UnexpectedExceptionHandler
{
    public static async Task AbortFailedResponseAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (context.Response.HasStarted)
        {
            // UseExceptionHandler cannot replace a started response and logs its raw exception.
            if (!context.RequestAborted.IsCancellationRequested || exception is not (OperationCanceledException or IOException))
            {
                LogFailure(context, exception);
            }

            context.Abort();
        }
    }

    public static Task WriteAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is BadHttpRequestException badRequest)
        {
            context.Response.StatusCode = badRequest.StatusCode;
            return Task.CompletedTask;
        }

        LogFailure(context, exception);

        var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<BusinessLogicExceptionMiddleware>>();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(new
        {
            Code = ErrorCodes.InternalServerError,
            Message = localizer[ErrorCodes.InternalServerError].Value,
            TraceId = context.TraceIdentifier,
        }, context.RequestAborted);
    }

    private static void LogFailure(HttpContext context, Exception? exception)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(UnexpectedExceptionHandler));

        // Exception messages can include request data. Log the type and correlate via the trace ID.
        RequestFailed(logger, exception?.GetType().FullName, context.TraceIdentifier);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled request exception {ExceptionType} ({TraceId})")]
    private static partial void RequestFailed(ILogger logger, string? exceptionType, string traceId);
}
