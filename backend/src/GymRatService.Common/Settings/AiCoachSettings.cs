namespace GymRatService.Common.Settings;

public sealed class AiCoachSettings
{
    public const string SectionName = "AiCoach";

    public const string GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/";

    public string Model { get; set; } = "gemini-2.5-flash-lite";

    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = GeminiEndpoint;

    public int MaximumMessageLength { get; set; } = 1000;

    public int HistoryLimit { get; set; } = 10;

    public int RateLimitRequests { get; set; } = 20;

    public int RateLimitWindowSeconds { get; set; } = 60;

    public int DailyTokenBudget { get; set; } = 50000;

    public int MaximumOutputTokens { get; set; } = 600;
}
