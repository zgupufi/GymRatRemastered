using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GymRatService.API.OpenApi;

public sealed class NumericEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type.IsEnum && schema.Type is { } schemaType && schemaType.HasFlag(JsonSchemaType.Integer))
        {
            schema.Enum = [.. Enum.GetValuesAsUnderlyingType(type).Cast<object>().Select(value => (JsonNode)JsonValue.Create(Convert.ToInt32(value)))];
        }

        return Task.CompletedTask;
    }
}
