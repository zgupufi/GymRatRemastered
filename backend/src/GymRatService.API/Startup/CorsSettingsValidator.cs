using GymRatService.API.Extensions;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Startup;

public sealed class CorsSettingsValidator(IHostEnvironment environment) : IValidateOptions<CorsSettings>
{
    private const string AllowedOriginsSetting = $"{CorsSettings.SectionName}:{nameof(CorsSettings.AllowedOrigins)}";

    private const string SameOriginSetting = $"{CorsSettings.SectionName}:{nameof(CorsSettings.SameOrigin)}";

    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        if (environment.AllowsDevelopmentSettings() || options.SameOrigin)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.AllowedOrigins.All(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail($"{AllowedOriginsSetting} needs at least one origin. Set it to the origin of the web app, or set {SameOriginSetting} to true when the web app and the API are served from the same origin.");
        }

        return ValidateOptionsResult.Success;
    }
}
