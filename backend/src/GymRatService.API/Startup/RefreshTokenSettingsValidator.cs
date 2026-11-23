using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Startup;

public sealed class RefreshTokenSettingsValidator : IValidateOptions<RefreshTokenSettings>
{
    private const string Section = RefreshTokenSettings.SectionName;

    public ValidateOptionsResult Validate(string? name, RefreshTokenSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.CookieName))
        {
            failures.Add($"{Section}:{nameof(RefreshTokenSettings.CookieName)} must not be empty.");
        }

        if (options.LifetimeDays <= 0)
        {
            failures.Add($"{Section}:{nameof(RefreshTokenSettings.LifetimeDays)} must be greater than zero.");
        }

        if (options.AbsoluteLifetimeDays < options.LifetimeDays)
        {
            failures.Add($"{Section}:{nameof(RefreshTokenSettings.AbsoluteLifetimeDays)} must not be smaller than {nameof(RefreshTokenSettings.LifetimeDays)}.");
        }

        if (options.ReuseGraceSeconds < 0)
        {
            failures.Add($"{Section}:{nameof(RefreshTokenSettings.ReuseGraceSeconds)} must not be negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
