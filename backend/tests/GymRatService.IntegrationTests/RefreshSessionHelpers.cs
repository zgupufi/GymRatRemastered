using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GymRatService.IntegrationTests;

public sealed record RefreshSession(string AccessToken, string RefreshToken);

public static class RefreshSessionHelpers
{
    public const string RefreshCookieName = "gymrat_refresh";

    public static HttpClient CreateCookielessClient(this GymRatApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public static async Task RegisterAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, password, "Ana", null, WeightUnit.Kilograms));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public static async Task<RefreshSession> LoginWithRefreshAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        return await ReadSessionAsync(response);
    }

    public static async Task<HttpResponseMessage> SendRefreshAsync(this HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");

        return await client.SendAsync(request);
    }

    public static async Task<RefreshSession> RefreshAsync(this HttpClient client, string refreshToken)
    {
        return await ReadSessionAsync(await client.SendRefreshAsync(refreshToken));
    }

    private static async Task<RefreshSession> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal));
        var separator = header.IndexOf(';', StringComparison.Ordinal);
        var pair = separator < 0 ? header : header[..separator];

        return new RefreshSession(body!.AccessToken, pair[(RefreshCookieName.Length + 1)..]);
    }
}
