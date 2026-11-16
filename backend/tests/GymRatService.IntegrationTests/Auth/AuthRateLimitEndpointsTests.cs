using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;

namespace GymRatService.IntegrationTests.Auth;

public sealed class AuthRateLimitEndpointsTests : IDisposable
{
    private const string Password = "password123";

    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("en", "Too many attempts. Please wait a little and try again.")]
    [InlineData("ro", "Prea multe încercări. Te rugăm să aștepți puțin și să încerci din nou.")]
    public async Task Login_RefusesTheEleventhAttemptInAMinuteFromOneIp_AndLeavesOtherIpsAlone(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await RegisterAsync(client, "ana@example.com");
        var allowed = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var allowedResponse = await LoginAsync(client, "ana@example.com", Password);
            allowed.Add(allowedResponse.StatusCode);
        }

        // Act
        var response = await LoginAsync(client, "ana@example.com", Password);

        // Assert
        Assert.All(allowed, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.TooManyAttempts, expectedMessage), error);
        Assert.InRange(GetRetryAfterSeconds(response), 1, 60);

        var otherIpResponse = await LoginAsync(_factory.CreateClientWithIp("203.0.113.11"), "ana@example.com", Password);
        var login = await otherIpResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal(HttpStatusCode.OK, otherIpResponse.StatusCode);
        Assert.False(string.IsNullOrEmpty(login?.AccessToken));
    }

    [Fact]
    public async Task Register_RefusesTheSixthRegistrationInAMinuteFromOneIp_AndLeavesOtherIpsAlone()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");
        var allowed = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var allowedResponse = await RegisterAsync(client, $"user{attempt}@example.com");
            allowed.Add(allowedResponse.StatusCode);
        }

        // Act
        var response = await RegisterAsync(client, "refused@example.com");

        // Assert
        Assert.All(allowed, status => Assert.Equal(HttpStatusCode.Created, status));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);
        Assert.InRange(GetRetryAfterSeconds(response), 1, 60);

        var otherIpClient = _factory.CreateClientWithIp("203.0.113.11");
        var refusedLogin = await LoginAsync(otherIpClient, "refused@example.com", Password);
        var refusedLoginError = await refusedLogin.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidCredentials, refusedLoginError?.Code);

        var otherIpResponse = await RegisterAsync(otherIpClient, "maria@example.com");
        Assert.Equal(HttpStatusCode.Created, otherIpResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DoesNotUseUpTheLoginLimitOfTheSameIp()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await RegisterAsync(client, $"user{attempt}@example.com");
        }

        // Act
        var response = await LoginAsync(client, "user0@example.com", Password);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(login?.AccessToken));
    }

    [Fact]
    public async Task Login_SharesOneLimit_WhenTheClientIpIsUnknown()
    {
        // Arrange
        _factory.Settings["AuthRateLimit:LoginPermitLimit"] = "2";
        await _factory.SeedAsync(_ => { });
        await RegisterAsync(_factory.CreateClient(), "ana@example.com");
        await LoginAsync(_factory.CreateClient(), "ana@example.com", Password);
        await LoginAsync(_factory.CreateClient(), "ana@example.com", Password);

        // Act
        var response = await LoginAsync(_factory.CreateClient(), "ana@example.com", Password);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);
    }

    [Fact]
    public async Task Login_UsesTheConfiguredLimitsAndWindow()
    {
        // Arrange
        _factory.Settings["AuthRateLimit:LoginPermitLimit"] = "1";
        _factory.Settings["AuthRateLimit:LoginWindowSeconds"] = "1";
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");
        await RegisterAsync(client, "ana@example.com");
        await LoginAsync(client, "ana@example.com", Password);
        var refused = await LoginAsync(client, "ana@example.com", Password);

        // Act
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var response = await LoginAsync(client, "ana@example.com", Password);

        // Assert
        var refusedError = await refused.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, refusedError?.Code);
        Assert.Equal(1, GetRetryAfterSeconds(refused));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(login?.AccessToken));
    }

    [Fact]
    public async Task Login_RefusesAnEmailAfterFiveFailedAttemptsAcrossIps_EvenWithTheRightPassword()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        await RegisterAsync(_factory.CreateClientWithIp("203.0.113.1"), "ana@example.com");
        var failures = new List<string?>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failedResponse = await LoginAsync(_factory.CreateClientWithIp($"203.0.113.{10 + attempt}"), " ANA@example.com", "wrong-password");
            var failedError = await failedResponse.Content.ReadFromJsonAsync<ErrorResponse>();
            failures.Add(failedError?.Code);
        }

        // Act
        var response = await LoginAsync(_factory.CreateClientWithIp("203.0.113.99"), "ana@example.com", Password);

        // Assert
        Assert.All(failures, code => Assert.Equal(ErrorCodes.InvalidCredentials, code));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.TooManyAttempts, "Too many attempts. Please wait a little and try again."), error);
        Assert.InRange(GetRetryAfterSeconds(response), 840, 900);
    }

    [Fact]
    public async Task Login_LimitsFailedAttemptsForAnEmailWithoutAnAccount()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(client, "nobody@example.com", "wrong-password");
        }

        // Act
        var response = await LoginAsync(client, "nobody@example.com", "wrong-password");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);
    }

    [Fact]
    public async Task Login_KeepsCountingFailuresForAnEmail_WhenAnotherUserLogsInSuccessfully()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");
        await RegisterAsync(client, "ana@example.com");
        await RegisterAsync(client, "maria@example.com");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await LoginAsync(client, "ana@example.com", "wrong-password");
        }

        var otherUserResponse = await LoginAsync(client, "maria@example.com", Password);
        await LoginAsync(client, "ana@example.com", "wrong-password");

        // Act
        var response = await LoginAsync(client, "ana@example.com", Password);

        // Assert
        Assert.Equal(HttpStatusCode.OK, otherUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);

        var otherUserAgain = await LoginAsync(client, "maria@example.com", Password);
        var login = await otherUserAgain.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(login?.AccessToken));
    }

    [Fact]
    public async Task Login_StartsCountingFailuresAgain_AfterTheSameUserLogsInSuccessfully()
    {
        // Arrange
        _factory.Settings["AuthRateLimit:LoginPermitLimit"] = "100";
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");
        await RegisterAsync(client, "ana@example.com");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await LoginAsync(client, "ana@example.com", "wrong-password");
        }

        await LoginAsync(client, "ana@example.com", Password);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await LoginAsync(client, "ana@example.com", "wrong-password");
        }

        // Act
        var response = await LoginAsync(client, "ana@example.com", Password);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(login?.AccessToken));
    }

    [Fact]
    public async Task Login_UsesTheConfiguredFailedLoginLimit()
    {
        // Arrange
        _factory.Settings["AuthRateLimit:FailedLoginLimit"] = "1";
        _factory.Settings["AuthRateLimit:FailedLoginWindowSeconds"] = "30";
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClientWithIp("203.0.113.10");
        await RegisterAsync(client, "ana@example.com");
        await LoginAsync(client, "ana@example.com", "wrong-password");

        // Act
        var response = await LoginAsync(client, "ana@example.com", Password);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);
        Assert.InRange(GetRetryAfterSeconds(response), 1, 30);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password, "Ana", null, WeightUnit.Kilograms));

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private static int GetRetryAfterSeconds(HttpResponseMessage response) =>
        (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0);
}
