namespace GymRatService.Common.Settings;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    public string CookieName { get; set; } = "gymrat_refresh";

    public int LifetimeDays { get; set; } = 30;

    public int AbsoluteLifetimeDays { get; set; } = 90;

    public int ReuseGraceSeconds { get; set; } = 10;

    public RefreshCookieSameSite SameSite { get; set; } = RefreshCookieSameSite.Strict;
}
