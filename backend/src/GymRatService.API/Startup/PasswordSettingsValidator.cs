using GymRatService.API.Extensions;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Startup;

public sealed class PasswordSettingsValidator(IHostEnvironment environment) : IValidateOptions<PasswordSettings>
{
    private const string PepperSetting = $"{PasswordSettings.SectionName}:{nameof(PasswordSettings.Pepper)}";

    public ValidateOptionsResult Validate(string? name, PasswordSettings options)
    {
        if (environment.AllowsDevelopmentSettings())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.Pepper))
        {
            return ValidateOptionsResult.Fail($"{PepperSetting} is required. Generate a random secret and set it before starting the API.");
        }

        if (DevelopmentSecrets.Contains(options.Pepper))
        {
            return ValidateOptionsResult.Fail($"{PepperSetting} is set to a publicly known development value. Generate a random secret and set it before starting the API.");
        }

        return ValidateOptionsResult.Success;
    }
}
