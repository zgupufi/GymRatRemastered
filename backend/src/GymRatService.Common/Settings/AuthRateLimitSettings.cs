namespace GymRatService.Common.Settings;

public sealed class AuthRateLimitSettings
{
    public const string SectionName = "AuthRateLimit";

    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowSeconds { get; set; } = 60;

    public int FailedLoginLimit { get; set; } = 5;

    public int FailedLoginWindowSeconds { get; set; } = 900;
}
