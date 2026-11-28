using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymRatService.Common.Enums;

namespace GymRatService.IntegrationTests.Platform;

public sealed class PlatformEndpointsTests
{
    [Fact]
    public async Task GetOpenApiDocument_ReturnsTheDocument_InDevelopment()
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment: "Development");
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/Exercises", out _));
    }

    [Fact]
    public async Task GetOpenApiDocument_DescribesTheResponsesAsTheApiSerializesThem()
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment: "Development");
        var client = factory.CreateClient();

        // Act
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        // Assert
        var schemas = document.GetProperty("components").GetProperty("schemas");
        var profileResponse = document.GetProperty("paths").GetProperty("/api/Users/me").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("#/components/schemas/UserProfileDto", profileResponse.GetProperty("$ref").GetString());
        var restTime = schemas.GetProperty("UserProfileDto").GetProperty("properties").GetProperty("defaultRestTimeSeconds");
        Assert.Equal("integer", restTime.GetProperty("type").GetString());
        Assert.False(restTime.TryGetProperty("pattern", out _));
        var stepKg = schemas.GetProperty("WorkoutExerciseRequest").GetProperty("properties").GetProperty("weightStepKg");
        Assert.Equal(["null", "number"], stepKg.GetProperty("type").EnumerateArray().Select(type => type.GetString()).Order());
        Assert.Equal([0, 1], schemas.GetProperty("WeightUnit").GetProperty("enum").EnumerateArray().Select(value => value.GetInt32()));
        var statuses = schemas.GetProperty("SuggestionStatus").GetProperty("enum").EnumerateArray().Select(value => value.GetString());
        Assert.Equal(Enum.GetNames<SuggestionStatus>(), statuses);
    }

    [Fact]
    public async Task GetOpenApiDocument_ReturnsNotFound_OutsideDevelopment()
    {
        // Arrange
        using var factory = new GymRatApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetExercises_Throws_WhenTheFailureIsNotAServiceException()
    {
        // Arrange
        using var factory = new GymRatApiFactory();
        var client = factory.CreateAuthenticatedClient();

        // Act
        var request = client.GetAsync("/api/exercises");

        // Assert
        await Assert.ThrowsAnyAsync<Exception>(() => request);
    }
}
