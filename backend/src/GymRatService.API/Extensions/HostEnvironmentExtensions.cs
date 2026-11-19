namespace GymRatService.API.Extensions;

public static class HostEnvironmentExtensions
{
    public const string TestingEnvironment = "Testing";

    public static bool AllowsDevelopmentSettings(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironment);
}
