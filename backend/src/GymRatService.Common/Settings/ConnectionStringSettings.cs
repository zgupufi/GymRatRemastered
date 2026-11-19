namespace GymRatService.Common.Settings;

public sealed class ConnectionStringSettings
{
    public const string SectionName = "ConnectionStrings";

    public string DefaultConnection { get; set; } = string.Empty;
}
