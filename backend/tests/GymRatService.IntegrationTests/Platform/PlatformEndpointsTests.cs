using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
