namespace GymRatService.API.Extensions;

public static class TransportSecurityExtensions
{
    public static IServiceCollection AddTransportSecurity(this IServiceCollection services)
    {
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = false;
            options.Preload = false;
        });

        return services;
    }

    public static WebApplication UseTransportSecurity(this WebApplication app)
    {
        if (!app.Environment.AllowsDevelopmentSettings())
        {
            app.UseHsts();
        }

        return app;
    }
}
