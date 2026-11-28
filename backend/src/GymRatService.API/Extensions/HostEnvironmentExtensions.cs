using System.Reflection;

namespace GymRatService.API.Extensions;

public static class HostEnvironmentExtensions
{
    public const string TestingEnvironment = "Testing";

    private const string OpenApiGeneratorAssembly = "GetDocument.Insider";

    public static bool AllowsDevelopmentSettings(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironment);

    public static bool IsGeneratingOpenApiDocument(this IHostEnvironment environment) =>
        Assembly.GetEntryAssembly()?.GetName().Name == OpenApiGeneratorAssembly;
}
