using GymRatService.API.Extensions;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Startup;

public sealed class ConnectionStringSettingsValidator(IHostEnvironment environment) : IValidateOptions<ConnectionStringSettings>
{
    private const string DefaultConnectionSetting = $"{ConnectionStringSettings.SectionName}:{nameof(ConnectionStringSettings.DefaultConnection)}";

    public ValidateOptionsResult Validate(string? name, ConnectionStringSettings options)
    {
        if (environment.AllowsDevelopmentSettings())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.DefaultConnection))
        {
            return ValidateOptionsResult.Fail($"{DefaultConnectionSetting} is required. Set the PostgreSQL connection string before starting the API.");
        }

        return ValidateOptionsResult.Success;
    }
}
