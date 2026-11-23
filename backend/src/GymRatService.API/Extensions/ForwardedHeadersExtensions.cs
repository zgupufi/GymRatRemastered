using GymRatService.Common.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddTrustedForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersSettings>(configuration.GetSection(ForwardedHeadersSettings.SectionName));

        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();

                foreach (var network in settings.Value.TrustedNetworks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        return services;
    }

    public static WebApplication UseTrustedForwardedHeaders(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value;

        if (settings.TrustedNetworks.Length > 0)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
