using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
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

    [Fact]
    public async Task UpdateUserSettings_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.PatchAsJsonAsync("/api/users/me/settings", new UpdateUserSettingsRequest(WeightUnit.Pounds, 120));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserSettings_UpdatesBothSettingsOfTheLoggedInUserOnly()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var otherAccessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("other@example.com", "password123", "Other", null, WeightUnit.Kilograms));
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.PatchAsJsonAsync("/api/users/me/settings", new UpdateUserSettingsRequest(WeightUnit.Pounds, 150));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var profile = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Ana", null, "ana@example.com", WeightUnit.Pounds, 150), profile);
        var otherProfile = await _factory.CreateBearerClient(otherAccessToken).GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Other", null, "other@example.com", WeightUnit.Kilograms, 90), otherProfile);
    }

    [Theory]
    [InlineData("{\"defaultRestTimeSeconds\":45}", WeightUnit.Kilograms, 45)]
    [InlineData("{\"weightUnit\":1}", WeightUnit.Pounds, 90)]
    [InlineData("{}", WeightUnit.Kilograms, 90)]
    public async Task UpdateUserSettings_KeepsTheSettingsThatAreNotSent(
        string body,
        WeightUnit expectedWeightUnit,
        int expectedDefaultRestTimeSeconds)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.PatchAsync(
            "/api/users/me/settings",
            new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var profile = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Ana", null, "ana@example.com", expectedWeightUnit, expectedDefaultRestTimeSeconds), profile);
    }

    [Fact]
    public async Task UpdateUserSettings_ReturnsBadRequestWithEveryValidationError_WhenTheSettingsAreInvalid()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.PatchAsJsonAsync("/api/users/me/settings", new UpdateUserSettingsRequest((WeightUnit)5, 601));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error.Code);
        Assert.Equal("The request is not valid.", error.Message);
        Assert.Equal(
        [
            new ValidationError("WeightUnit", "'Weight Unit' has a range of values which does not include '5'."),
            new ValidationError("DefaultRestTimeSeconds", "'Default Rest Time Seconds' must be between 30 and 600. You entered 601."),
        ], error.Errors);
        var profile = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Ana", null, "ana@example.com", WeightUnit.Kilograms, 90), profile);
    }

    [Fact]
    public async Task UpdateUserSettings_ReturnsRomanianValidationMessages_WhenTheLanguageIsRomanian()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ro");

        // Act
        var response = await client.PatchAsJsonAsync("/api/users/me/settings", new UpdateUserSettingsRequest(null, 10));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error.Code);
        Assert.Equal("Cererea nu este validă.", error.Message);
        var validationError = Assert.Single(error.Errors!);
        Assert.Equal("DefaultRestTimeSeconds", validationError.Property);
        Assert.Equal("'Default Rest Time Seconds' trebuie sa fie între 30 şi 600. Ai introdus 10.", validationError.Message);
    }

    [Theory]
    [InlineData("en", "The user does not exist.")]
    [InlineData("ro", "Utilizatorul nu există.")]
    public async Task UpdateUserSettings_ReturnsBadRequestWithUserNotFound_WhenTheTokenBelongsToAMissingUser(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = GymRatApiFactory.CreateAccessToken(new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()));
        var client = _factory.CreateBearerClient(accessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.PatchAsJsonAsync("/api/users/me/settings", new UpdateUserSettingsRequest(WeightUnit.Pounds, 120));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.UserNotFound, expectedMessage), error);
    }

[Theory]
    [InlineData("POST", "/api/users/me/password")]
    [InlineData("DELETE", "/api/users/me")]
    public async Task AccountEndpoints_ReturnUnauthorized_WhenTheCallerIsNotAuthenticated(string method, string url)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new DeleteAccountRequest("password123")),
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReplacesThePassword_WhenTheCurrentOneMatches()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest("password123", "new-password-456"));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var anonymous = _factory.CreateClient();
        var oldLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@example.com", "password123"));
        var oldError = await oldLogin.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, oldLogin.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, oldError?.Code);
        var newLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@example.com", "new-password-456"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Theory]
    [InlineData("en", "The password is incorrect.")]
    [InlineData("ro", "Parola este incorectă.")]
    public async Task ChangePassword_ReturnsBadRequestWithInvalidPassword_WhenTheCurrentOneIsWrong(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest("wrong-password", "new-password-456"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.InvalidPassword, expectedMessage), error);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@example.com", "password123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequestWithValidationErrors_WhenTheNewPasswordIsTooShort()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest("password123", "short"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error.Code);
        Assert.Equal("NewPassword", Assert.Single(error.Errors!).Property);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequestWithTooManyAttempts_AfterRepeatedWrongPasswords()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);
        HttpResponseMessage? response = null;

        // Act
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest("wrong-password", "new-password-456"));
        }

        // Assert
        Assert.NotNull(response);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);
    }

    [Fact]
    public async Task DeleteAccount_RemovesTheUserAndTheirData_WhenThePasswordMatches()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var otherToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("other@example.com", "password123", "Other", null, WeightUnit.Kilograms));
        var otherClient = _factory.CreateBearerClient(otherToken);
        await otherClient.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest("Other Day"));
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);
        await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest("Push Day"));

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("password123")),
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var profile = await client.GetAsync("/api/users/me");
        var profileError = await profile.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.UserNotFound, profileError?.Code);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@example.com", "password123"));
        var loginError = await login.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidCredentials, loginError?.Code);
        var otherWorkouts = await otherClient.GetFromJsonAsync<PagedResult<WorkoutSummaryDto>>("/api/workouts");
        Assert.Equal("Other Day", Assert.Single(otherWorkouts!.Items).Name);
        var reRegistered = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var freshWorkouts = await _factory.CreateBearerClient(reRegistered).GetFromJsonAsync<PagedResult<WorkoutSummaryDto>>("/api/workouts");
        Assert.Empty(freshWorkouts!.Items);
    }

    [Theory]
    [InlineData("en", "The password is incorrect.")]
    [InlineData("ro", "Parola este incorectă.")]
    public async Task DeleteAccount_ReturnsBadRequestWithInvalidPassword_AndKeepsTheUser_WhenThePasswordIsWrong(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
        var client = _factory.CreateBearerClient(accessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("wrong-password")),
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.InvalidPassword, expectedMessage), error);
        var profile = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsBadRequestWithUserNotFound_WhenTheTokenBelongsToAMissingUser()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = GymRatApiFactory.CreateAccessToken(new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("password123")),
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.UserNotFound, error?.Code);
    }
}
