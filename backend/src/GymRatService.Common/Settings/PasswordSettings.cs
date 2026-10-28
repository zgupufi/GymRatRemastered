namespace GymRatService.Common.Settings;

public sealed class PasswordSettings
{
    public const string SectionName = "PasswordSettings";

    public string Pepper { get; set; } = string.Empty;
}
