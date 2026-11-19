using System.Text;
using GymRatService.API.Extensions;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Startup;

public sealed class JwtSettingsValidator(IHostEnvironment environment) : IValidateOptions<JwtSettings>
{
    public const int MinimumKeyBytes = 32;

    private const string KeySetting = $"{JwtSettings.SectionName}:{nameof(JwtSettings.Key)}";

    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (environment.AllowsDevelopmentSettings())
        {
            return ValidateOptionsResult.Success;
        }

        if (Encoding.UTF8.GetByteCount(options.Key.Trim()) < MinimumKeyBytes)
        {
            return ValidateOptionsResult.Fail($"{KeySetting} must be at least {MinimumKeyBytes} bytes long. Generate a random secret and set it before starting the API.");
        }

        if (DevelopmentSecrets.Contains(options.Key))
        {
            return ValidateOptionsResult.Fail($"{KeySetting} is set to a publicly known development value. Generate a random secret and set it before starting the API.");
        }

        return ValidateOptionsResult.Success;
    }
}
