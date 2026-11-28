using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GymRatService.API.OpenApi;

public sealed class NumberSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Type is { } type && type.HasFlag(JsonSchemaType.String) && (type & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0)
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
