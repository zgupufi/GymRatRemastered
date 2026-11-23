using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GymRatService.IntegrationTests.Users;

public sealed class UserSessionRevocationEndpointsTests : IDisposable
{
    private const string CookieName = "gymrat_refresh";

    private const string Email = "ana@example.com";

    private const string Password = "password123";

    private const string NewPassword = "new-password-456";

    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ChangePassword_RevokesEveryRefreshSession_WhenTheCurrentPasswordMatches()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var anonymous = CreateCookielessClient();
        await RegisterAsync(anonymous);
        var first = await LoginAsync(anonymous);
        var second = await LoginAsync(anonymous);
        var client = _factory.CreateBearerClient(first.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(Password, NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertRefreshRejectedAsync(anonymous, first.RefreshToken);
        await AssertRefreshRejectedAsync(anonymous, second.RefreshToken);
    }

    [Fact]
    public async Task ChangePassword_RevokesRotatedSessions_AndStillAllowsLoginWithTheNewPassword()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var anonymous = CreateCookielessClient();
        await RegisterAsync(anonymous);
        var login = await LoginAsync(anonymous);
        var rotated = await RefreshAsync(anonymous, login.RefreshToken);
        var client = _factory.CreateBearerClient(rotated.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(Password, NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertRefreshRejectedAsync(anonymous, rotated.RefreshToken);
        var newLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, NewPassword));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var fresh = ReadCookie(newLogin);
        var refreshed = await SendRefreshAsync(anonymous, fresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_KeepsTheRefreshSessions_WhenTheCurrentPasswordIsWrong()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var anonymous = CreateCookielessClient();
        await RegisterAsync(anonymous);
        var login = await LoginAsync(anonymous);
        var client = _factory.CreateBearerClient(login.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest("wrong-password", NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var refreshed = await SendRefreshAsync(anonymous, login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_DoesNotRevokeTheSessionsOfOtherUsers()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var anonymous = CreateCookielessClient();
        await RegisterAsync(anonymous);
        await RegisterAsync(anonymous, "other@example.com");
        var login = await LoginAsync(anonymous);
        var other = await LoginAsync(anonymous, "other@example.com");
        var client = _factory.CreateBearerClient(login.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(Password, NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var refreshed = await SendRefreshAsync(anonymous, other.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RemovesEveryRefreshSession_WhenThePasswordMatches()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var anonymous = CreateCookielessClient();
        await RegisterAsync(anonymous);
        var first = await LoginAsync(anonymous);
        var second = await LoginAsync(anonymous);
        var client = _factory.CreateBearerClient(first.AccessToken);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(Password)),
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertRefreshRejectedAsync(anonymous, first.RefreshToken);
        await AssertRefreshRejectedAsync(anonymous, second.RefreshToken);
    }

    [Fact]
    public async Task DeleteAccount_KeepsTheRefreshSessions_WhenThePasswordIsWrong()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var anonymous = CreateCookielessClient();
        await RegisterAsync(anonymous);
        var login = await LoginAsync(anonymous);
        var client = _factory.CreateBearerClient(login.AccessToken);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("wrong-password")),
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var refreshed = await SendRefreshAsync(anonymous, login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    private HttpClient CreateCookielessClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    private static async Task RegisterAsync(HttpClient client, string email = Email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, Password, "Ana", null, WeightUnit.Kilograms));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<(string AccessToken, string RefreshToken)> LoginAsync(HttpClient client, string email = Email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return (body!.AccessToken, ReadCookie(response));
    }

    private static async Task<(string AccessToken, string RefreshToken)> RefreshAsync(HttpClient client, string token)
    {
        var response = await SendRefreshAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return (body!.AccessToken, ReadCookie(response));
    }

    private static async Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{CookieName}={token}");

        return await client.SendAsync(request);
    }

    private static async Task AssertRefreshRejectedAsync(HttpClient client, string token)
    {
        var response = await SendRefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRefreshToken, error?.Code);
    }

    private static string ReadCookie(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith($"{CookieName}=", StringComparison.Ordinal));
        var separator = header.IndexOf(';', StringComparison.Ordinal);
        var pair = separator < 0 ? header : header[..separator];

        return pair[(CookieName.Length + 1)..];
    }
}
