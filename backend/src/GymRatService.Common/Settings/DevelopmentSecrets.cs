namespace GymRatService.Common.Settings;

public static class DevelopmentSecrets
{
    private static readonly HashSet<string> KnownValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "gymrat-development-only-jwt-signing-key",
        "gymrat-development-only-password-pepper",
        "change-me-to-a-random-string-of-at-least-32-characters",
        "change-me-to-a-different-random-string",
        "integration-test-signing-key-that-is-long-enough",
        "integration-test-pepper",
    };

    public static bool Contains(string value) => KnownValues.Contains(value.Trim());
}
