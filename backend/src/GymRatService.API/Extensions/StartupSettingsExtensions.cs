using GymRatService.API.Startup;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Extensions;

public static class StartupSettingsExtensions
{
    public static IServiceCollection AddStartupSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddSingleton<IValidateOptions<PasswordSettings>, PasswordSettingsValidator>();
        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();
        services.AddSingleton<IValidateOptions<ConnectionStringSettings>, ConnectionStringSettingsValidator>();

        services.AddOptions<JwtSettings>().Bind(configuration.GetSection(JwtSettings.SectionName)).ValidateOnStart();
        services.AddOptions<PasswordSettings>().Bind(configuration.GetSection(PasswordSettings.SectionName)).ValidateOnStart();
        services.AddOptions<CorsSettings>().Bind(configuration.GetSection(CorsSettings.SectionName)).ValidateOnStart();
        services.AddOptions<ConnectionStringSettings>().Bind(configuration.GetSection(ConnectionStringSettings.SectionName)).ValidateOnStart();

        return services;
    }
}
