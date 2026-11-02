namespace GymRatService.Common.Settings;

public sealed class AiCoachSettings
{
    public const string SectionName = "AiCoach";

    public const string DefaultEndpoint = "https://openrouter.ai/api/v1";

    public string Model { get; set; } = "nvidia/nemotron-3-ultra-550b-a55b:free";

    public string FallbackModel { get; set; } = "google/gemma-4-31b-it:free";

    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = DefaultEndpoint;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int MaximumMessageLength { get; set; } = 1000;

    public int HistoryLimit { get; set; } = 10;

    public int MaximumToolIterations { get; set; } = 3;

    public int RateLimitRequests { get; set; } = 5;

    public int RateLimitWindowSeconds { get; set; } = 60;

    public int DailyRequestLimit { get; set; } = 100;

    public int DailyTokenBudget { get; set; } = 30000;

    public int MaximumOutputTokens { get; set; } = 600;
}
