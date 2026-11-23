using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Common.DTOs.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace GymRatService.IntegrationTests.Auth;

public sealed class AuthRefreshEndpointsTests : IDisposable
{
    private const string CookieName = "gymrat_refresh";

    private const string Email = "ana@example.com";

    private const string Password = "password123";

    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_SetsAnHttpOnlyRefreshCookieScopedToTheAuthRoutes_AndKeepsTheTokenOutOfTheBody()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);

        // Act
        var response = await LoginAsync(client);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = ReadCookie(response);
        Assert.NotEmpty(cookie.Value);
        Assert.Contains("httponly", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie.Attributes.Replace("samesite", string.Empty), StringComparison.OrdinalIgnoreCase);
        Assert.InRange(ReadExpiry(cookie), DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
        Assert.DoesNotContain(cookie.Value, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_IssuesAShortLivedAccessToken()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);

        // Act
        var response = await LoginAsync(client);

        // Assert
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.InRange(login.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task Refresh_ReturnsAWorkingAccessTokenAndRotatesTheCookie_WhenTheCookieIsValid()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);
        var loginCookie = ReadCookie(await LoginAsync(client));

        // Act
        var response = await RefreshAsync(client, loginCookie.Value);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(refreshed);
        var profile = await _factory.CreateBearerClient(refreshed.AccessToken).GetFromJsonAsync<UserProfileDto>("/api/users/me");
        Assert.Equal(new UserProfileDto("Ana", null, Email, WeightUnit.Kilograms, 90), profile);
        var rotated = ReadCookie(response);
        Assert.NotEqual(loginCookie.Value, rotated.Value);
        Assert.Contains("httponly", rotated.Attributes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_KeepsTheSessionAliveForThirtyDaysOfInactivityAtATime()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);
        var cookie = ReadCookie(await LoginAsync(client));

        // Act
        _factory.Time.Advance(TimeSpan.FromDays(20));
        var first = await RefreshAsync(client, cookie.Value);
        var firstCookie = ReadCookie(first);
        _factory.Time.Advance(TimeSpan.FromDays(20));
        var second = await RefreshAsync(client, firstCookie.Value);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.InRange(ReadExpiry(ReadCookie(second)), DateTimeOffset.UtcNow.AddDays(69), DateTimeOffset.UtcNow.AddDays(71));
    }

    [Fact]
    public async Task Refresh_ReturnsInvalidRefreshToken_WhenThe30DayLifeEndedWithoutUse()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);
        var cookie = ReadCookie(await LoginAsync(client));
        _factory.Time.Advance(TimeSpan.FromDays(31));

        // Act
        var response = await RefreshAsync(client, cookie.Value);

        // Assert
        await AssertInvalidRefreshTokenAsync(response);
    }

    [Theory]
    [InlineData("en", "The session has expired. Please log in again.")]
    [InlineData("ro", "Sesiunea a expirat. Te rugăm să te autentifici din nou.")]
    public async Task Refresh_ReturnsInvalidRefreshTokenInTheRequestedLanguage_WhenThereIsNoCookie(string language, string expectedMessage)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = CreateClient(_factory);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.PostAsync("/api/auth/refresh", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.InvalidRefreshToken, expectedMessage), error);
    }

    [Fact]
    public async Task Refresh_ReturnsInvalidRefreshToken_WhenTheCookieIsNotAKnownToken()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = CreateClient(_factory);

        // Act
        var response = await RefreshAsync(client, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

        // Assert
        await AssertInvalidRefreshTokenAsync(response);
    }

    [Fact]
    public async Task Refresh_RevokesTheWholeFamily_WhenARotatedTokenIsReusedAfterTheGracePeriod()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);
        var original = ReadCookie(await LoginAsync(client));
        var rotated = ReadCookie(await RefreshAsync(client, original.Value));
        _factory.Time.Advance(TimeSpan.FromSeconds(11));

        // Act
        var reuse = await RefreshAsync(client, original.Value);
        var afterReuse = await RefreshAsync(client, rotated.Value);

        // Assert
        await AssertInvalidRefreshTokenAsync(reuse);
        await AssertInvalidRefreshTokenAsync(afterReuse);
    }

    [Fact]
    public async Task Refresh_AcceptsARotatedTokenAgain_WhenTwoRequestsRaceWithinTheGracePeriod()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);
        var original = ReadCookie(await LoginAsync(client));
        var first = await RefreshAsync(client, original.Value);

        // Act
        var second = await RefreshAsync(client, original.Value);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var third = await RefreshAsync(client, ReadCookie(second).Value);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshTokenAndItsRotations_AndClearsTheCookie()
    {
        // Arrange
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);
        var original = ReadCookie(await LoginAsync(client));
        var rotated = ReadCookie(await RefreshAsync(client, original.Value));

        // Act
        var logout = await SendWithCookieAsync(client, "/api/auth/logout", rotated.Value);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = ReadCookie(logout);
        Assert.Empty(cleared.Value);
        Assert.Contains("path=/api/auth", cleared.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(ReadExpiry(cleared), DateTimeOffset.MinValue, DateTimeOffset.UtcNow);
        await AssertInvalidRefreshTokenAsync(await RefreshAsync(client, rotated.Value));
        await AssertInvalidRefreshTokenAsync(await RefreshAsync(client, original.Value));
    }

    [Fact]
    public async Task Logout_LeavesTheOtherDevicesSessionUsable()
    {
        // Arrange
        await RegisterAsync(_factory);
        var phone = CreateClient(_factory);
        var laptop = CreateClient(_factory);
        var phoneCookie = ReadCookie(await LoginAsync(phone));
        var laptopCookie = ReadCookie(await LoginAsync(laptop));

        // Act
        await SendWithCookieAsync(phone, "/api/auth/logout", phoneCookie.Value);

        // Assert
        await AssertInvalidRefreshTokenAsync(await RefreshAsync(phone, phoneCookie.Value));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(laptop, laptopCookie.Value)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Logout_ReturnsNoContent_WhenTheCookieIsMissingOrUnknown(bool withUnknownToken)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = CreateClient(_factory);
        var token = withUnknownToken ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : null;

        // Act
        var response = token is null
            ? await client.PostAsync("/api/auth/logout", null)
            : await SendWithCookieAsync(client, "/api/auth/logout", token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RefusesRequestsOverTheLimitOfOneIp()
    {
        // Arrange
        _factory.Settings["AuthRateLimit:RefreshPermitLimit"] = "2";
        await _factory.SeedAsync(_ => { });
        var client = CreateClient(_factory);
        await client.PostAsync("/api/auth/refresh", null);
        await client.PostAsync("/api/auth/refresh", null);

        // Act
        var response = await client.PostAsync("/api/auth/refresh", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.TooManyAttempts, error?.Code);
    }

    [Fact]
    public async Task Cookie_IsSecureAndFollowsTheConfiguredSameSite_OutsideDevelopment()
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment: "Production");
        factory.Settings["JwtSettings:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        factory.Settings["PasswordSettings:Pepper"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        factory.Settings["ConnectionStrings:DefaultConnection"] = "Host=database;Database=gymrat";
        factory.Settings["Cors:AllowedOrigins:0"] = "https://app.example.com";
        factory.Settings["RefreshToken:SameSite"] = "None";
        await RegisterAsync(factory);
        var client = CreateClient(factory);

        // Act
        var response = await LoginAsync(client);

        // Assert
        var cookie = ReadCookie(response);
        Assert.Contains("secure", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie.Attributes, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Lax", "samesite=lax")]
    [InlineData("Strict", "samesite=strict")]
    public async Task Cookie_UsesTheConfiguredSameSiteMode(string setting, string expected)
    {
        // Arrange
        _factory.Settings["RefreshToken:SameSite"] = setting;
        await RegisterAsync(_factory);
        var client = CreateClient(_factory);

        // Act
        var response = await LoginAsync(client);

        // Assert
        Assert.Contains(expected, ReadCookie(response).Attributes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_AllowsCredentialsForAnAllowedOrigin()
    {
        // Arrange
        _factory.Settings["Cors:AllowedOrigins:0"] = "https://app.example.com";
        await _factory.SeedAsync(_ => { });
        var client = CreateClient(_factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Origin", "https://app.example.com");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal("https://app.example.com", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Theory]
    [InlineData("RefreshToken:CookieName", " ", "CookieName")]
    [InlineData("RefreshToken:LifetimeDays", "0", "LifetimeDays")]
    [InlineData("RefreshToken:ReuseGraceSeconds", "-1", "ReuseGraceSeconds")]
    public void Start_Fails_WhenARefreshTokenSettingIsInvalid(string setting, string value, string expectedName)
    {
        // Arrange
        _factory.Settings[setting] = value;

        // Act
        var exception = Assert.Throws<OptionsValidationException>(() => _factory.CreateClient());

        // Assert
        var failure = Assert.Single(exception.Failures);
        Assert.Contains(expectedName, failure);
    }

    private static HttpClient CreateClient(GymRatApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    private static async Task RegisterAsync(GymRatApiFactory factory)
    {
        await factory.SeedAsync(_ => { });

        var response = await CreateClient(factory).PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(Email, Password, "Ana", null, WeightUnit.Kilograms));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        return client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, Password));
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token)
    {
        return SendWithCookieAsync(client, "/api/auth/refresh", token);
    }

    private static async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"{CookieName}={token}");

        return await client.SendAsync(request);
    }

    private static async Task AssertInvalidRefreshTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRefreshToken, error?.Code);
    }

    private static (string Value, string Attributes) ReadCookie(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith($"{CookieName}=", StringComparison.Ordinal));
        var separator = header.IndexOf(';', StringComparison.Ordinal);
        var pair = separator < 0 ? header : header[..separator];
        var attributes = separator < 0 ? string.Empty : header[separator..];

        return (pair[(CookieName.Length + 1)..], attributes);
    }

    private static DateTimeOffset ReadExpiry((string Value, string Attributes) cookie)
    {
        var attribute = cookie.Attributes
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));

        return DateTimeOffset.Parse(attribute["expires=".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }
}
