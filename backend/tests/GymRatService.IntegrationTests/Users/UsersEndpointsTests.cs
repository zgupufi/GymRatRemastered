using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.IntegrationTests.Authentication;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Users;

public sealed class UsersEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenTheTokenIsNotValid()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateBearerClient("not-a-real-token");

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsTheProfileOfTheAuthenticatedUser()
    {
        // Arrange
        await _factory.SeedAsync(context => context.Users.AddRange(
            new User
            {
                Id = TestAuthHandler.UserId,
                Email = "ana@example.com",
                PasswordHash = "hash",
                PasswordSalt = "salt",
                FirstName = "Ana",
                WeightUnit = WeightUnit.Pounds,
                DefaultRestTimeSeconds = 150,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new User
            {
                Email = "other@example.com",
                PasswordHash = "hash",
                PasswordSalt = "salt",
                FirstName = "Other",
                CreatedAt = DateTimeOffset.UtcNow,
            }));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal(new UserProfileDto("Ana", null, "ana@example.com", WeightUnit.Pounds, 150), profile);
    }

    [Theory]
    [InlineData("en", "The user does not exist.")]
    [InlineData("ro", "Utilizatorul nu există.")]
    public async Task GetMe_ReturnsBadRequestWithUserNotFound_WhenTheUserNoLongerExists(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.UserNotFound, expectedMessage), error);
    }
}
