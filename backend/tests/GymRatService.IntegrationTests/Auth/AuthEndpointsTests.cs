using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.IntegrationTests.Auth;

public sealed class AuthEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Register_ReturnsCreatedAndTheNewUserCanLogIn()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(" Ana@Example.com ", "password123", " Ana ", " Pop ", WeightUnit.Pounds));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ana@example.com", "password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        var token = new JsonWebToken(login.AccessToken);
        Assert.Equal(7, Guid.Parse(token.Subject).Version);

        var profile = await _factory.CreateBearerClient(login.AccessToken).GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Ana", "Pop", "ana@example.com", WeightUnit.Pounds, 90), profile);
    }

    [Theory]
    [InlineData("en", "An account with this email already exists.")]
    [InlineData("ro", "Există deja un cont cu această adresă de email.")]
    public async Task Register_ReturnsBadRequestWithEmailAlreadyExists_WhenTheEmailIsTaken(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("ANA@example.com", "another-password", "Ana", null, WeightUnit.Kilograms));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.EmailAlreadyExists, expectedMessage), error);
    }

    [Fact]
    public async Task Register_ReturnsBadRequestWithEveryValidationError_WhenTheRequestIsInvalid()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("not-an-email", "1234567", "", null, WeightUnit.Kilograms));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error.Code);
        Assert.Equal("The request is not valid.", error.Message);
        Assert.Equal(
        [
            new ValidationError("Email", "'Email' is not a valid email address."),
            new ValidationError("Password", "The length of 'Password' must be at least 8 characters. You entered 7 characters."),
            new ValidationError("FirstName", "'First Name' must not be empty."),
        ], error.Errors);
    }

    [Fact]
    public async Task Register_ReturnsRomanianValidationMessages_WhenTheLanguageIsRomanian()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ro");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("ana@example.com", "password123", "", null, WeightUnit.Kilograms));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("Cererea nu este validă.", error.Message);
        var validationError = Assert.Single(error.Errors!);
        Assert.Equal("FirstName", validationError.Property);
        Assert.Equal("'First Name' nu ar trebui să fie goală.", validationError.Message);
    }

    [Fact]
    public async Task Login_ReturnsATokenThatAuthenticatesTheProfileEndpoint()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("ana@example.com", "password123", "Ana", "Pop", WeightUnit.Pounds));

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ANA@example.com", "password123"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.InRange(login.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(355), DateTimeOffset.UtcNow.AddMinutes(361));

        var profile = await _factory.CreateBearerClient(login.AccessToken).GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Ana", "Pop", "ana@example.com", WeightUnit.Pounds, 90), profile);
    }

    [Theory]
    [InlineData("ana@example.com", "wrong-password", "en", "The email or password is incorrect.")]
    [InlineData("nobody@example.com", "password123", "en", "The email or password is incorrect.")]
    [InlineData("ana@example.com", "wrong-password", "ro", "Adresa de email sau parola este incorectă.")]
    public async Task Login_ReturnsBadRequestWithInvalidCredentials_WhenTheCredentialsAreWrong(
        string email,
        string password,
        string language,
        string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.InvalidCredentials, expectedMessage), error);
    }
}
