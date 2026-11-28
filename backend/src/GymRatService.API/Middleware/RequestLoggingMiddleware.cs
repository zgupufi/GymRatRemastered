using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.API.Middleware;

public sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
            var pathTemplate = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
            var method = context.Request.Method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE"
                or "OPTIONS" or "CONNECT" or "TRACE" ? context.Request.Method : "OTHER";
            var userId = context.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var subject)
                ? subject
                : (Guid?)null;

            RequestCompleted(logger, method, pathTemplate, context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, userId, context.TraceIdentifier);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HTTP {Method} {PathTemplate} responded {StatusCode} in {DurationMs} ms for user {UserId} ({TraceId})")]
    private static partial void RequestCompleted(ILogger logger, string method, string? pathTemplate, int statusCode,
        double durationMs, Guid? userId, string traceId);
}
