using System.Net;
using System.Net.Sockets;
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

    private const string InvalidSettingsMessage = "Every AuthRateLimit value must be greater than zero.";

    private const int Ipv6PrefixBytes = 8;

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthRateLimitSettings>()
            .Bind(configuration.GetSection(AuthRateLimitSettings.SectionName))
            .Validate(HasOnlyPositiveValues, InvalidSettingsMessage)
            .ValidateOnStart();

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
        var client = GetClientKey(context.Connection.RemoteIpAddress);

        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit(settings),
            Window = TimeSpan.FromSeconds(windowSeconds(settings)),
            QueueLimit = 0,
        });
    }

    private static bool HasOnlyPositiveValues(AuthRateLimitSettings settings)
    {
        int[] values =
        [
            settings.LoginPermitLimit,
            settings.LoginWindowSeconds,
            settings.RegisterPermitLimit,
            settings.RegisterWindowSeconds,
            settings.FailedLoginLimit,
            settings.FailedLoginWindowSeconds,
        ];

        return values.All(value => value > 0);
    }

    private static string GetClientKey(IPAddress? address)
    {
        if (address is null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, Ipv6PrefixBytes, bytes.Length - Ipv6PrefixBytes);

        return new IPAddress(bytes).ToString();
    }
}
