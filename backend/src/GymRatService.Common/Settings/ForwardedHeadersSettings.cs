namespace GymRatService.Common.Settings;

public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public string[] TrustedNetworks { get; set; } = [];
}
