using GymRatService.API.OpenApi;

namespace GymRatService.API.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddApiDocument(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<NumberSchemaTransformer>();
            options.AddSchemaTransformer<NumericEnumSchemaTransformer>();
        });

        return services;
    }
}
