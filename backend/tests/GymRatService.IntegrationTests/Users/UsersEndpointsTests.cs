using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.IntegrationTests.Users;

public sealed class UsersEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetUserProfile_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated()
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
    public async Task GetUserProfile_ReturnsUnauthorized_WhenTheTokenIsNotValid()
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
    public async Task GetUserProfile_ReturnsTheProfileOfTheLoggedInUser()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        await _factory.RegisterAndLoginAsync(new RegisterRequest("other@example.com", "password123", "Other", null, WeightUnit.Kilograms));
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Pounds));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal(new UserProfileDto("Ana", null, "ana@example.com", WeightUnit.Pounds, 90), profile);
    }

    [Theory]
    [InlineData("en", "The user does not exist.")]
    [InlineData("ro", "Utilizatorul nu există.")]
    public async Task GetUserProfile_ReturnsBadRequestWithUserNotFound_WhenTheTokenBelongsToAMissingUser(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = GymRatApiFactory.CreateAccessToken(new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()));
        var client = _factory.CreateBearerClient(accessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.UserNotFound, expectedMessage), error);
    }

    [Theory]
    [InlineData("en", "The access token does not identify a user.")]
    [InlineData("ro", "Tokenul de acces nu identifică un utilizator.")]
    public async Task GetUserProfile_ReturnsBadRequestWithUnauthorizedAccess_WhenTheTokenHasNoUserId(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = GymRatApiFactory.CreateAccessToken(new Claim(JwtRegisteredClaimNames.Email, "ana@example.com"));
        var client = _factory.CreateBearerClient(accessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.GetAsync("/api/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.UnauthorizedAccess, expectedMessage), error);
    }
}
