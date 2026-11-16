using System.Threading.RateLimiting;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Extensions;

public static class RateLimitingExtensions
{
    public const string LoginPolicy = "AuthLogin";

    public const string RegisterPolicy = "AuthRegister";

    private const string UnknownClient = "unknown";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthRateLimitSettings>(configuration.GetSection(AuthRateLimitSettings.SectionName));

        services.AddRateLimiter(options =>
        {
            options.OnRejected = (context, _) =>
            {
                context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);

                throw new RateLimitedException(ErrorCodes.TooManyAttempts, retryAfter);
            };

            options.AddPolicy(LoginPolicy, context => CreateClientPartition(
                context,
                settings => settings.LoginPermitLimit,
                settings => settings.LoginWindowSeconds));

            options.AddPolicy(RegisterPolicy, context => CreateClientPartition(
                context,
                settings => settings.RegisterPermitLimit,
                settings => settings.RegisterWindowSeconds));
        });

        return services;
    }

    private static RateLimitPartition<string> CreateClientPartition(
        HttpContext context,
        Func<AuthRateLimitSettings, int> permitLimit,
        Func<AuthRateLimitSettings, int> windowSeconds)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitSettings>>().Value;
        var client = context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit(settings),
            Window = TimeSpan.FromSeconds(windowSeconds(settings)),
            QueueLimit = 0,
        });
    }
}
