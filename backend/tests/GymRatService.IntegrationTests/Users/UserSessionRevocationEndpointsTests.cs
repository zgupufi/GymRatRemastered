using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Errors;
using GymRatService.Repository;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.IntegrationTests.Users;

public sealed class UserSessionRevocationEndpointsTests : IDisposable
{
    private const string Email = "ana@example.com";

    private const string Password = "password123";

    private const string NewPassword = "new-password-456";

    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ChangePassword_RevokesEveryRefreshSession_WhenTheCurrentPasswordMatches()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        var first = await anonymous.LoginWithRefreshAsync(Email, Password);
        var second = await anonymous.LoginWithRefreshAsync(Email, Password);
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
        var anonymous = await CreateAnonymousClientWithUserAsync();
        var login = await anonymous.LoginWithRefreshAsync(Email, Password);
        var rotated = await anonymous.RefreshAsync(login.RefreshToken);
        var client = _factory.CreateBearerClient(rotated.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(Password, NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertRefreshRejectedAsync(anonymous, rotated.RefreshToken);
        var fresh = await anonymous.LoginWithRefreshAsync(Email, NewPassword);
        var refreshed = await anonymous.SendRefreshAsync(fresh.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_StampsEveryActiveSessionWithTheSameRevocationTime()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        var login = await anonymous.LoginWithRefreshAsync(Email, Password);
        await anonymous.LoginWithRefreshAsync(Email, Password);
        var client = _factory.CreateBearerClient(login.AccessToken);
        var before = DateTimeOffset.UtcNow;

        // Act
        await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(Password, NewPassword));

        // Assert
        var revokedAt = await ReadRevocationTimesAsync();
        Assert.Equal(2, revokedAt.Count);
        Assert.All(revokedAt, value => Assert.NotNull(value));
        Assert.Single(revokedAt.Distinct());
        Assert.InRange(revokedAt[0]!.Value, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task ChangePassword_KeepsThePasswordAndTheSessions_WhenRevokingFails()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        var login = await anonymous.LoginWithRefreshAsync(Email, Password);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER block_revoke BEFORE UPDATE OF RevokedAt ON RefreshTokens BEGIN SELECT RAISE(ABORT, 'revoke blocked'); END;");
        var repository = scope.ServiceProvider.GetRequiredService<IUsersRepository>();
        var user = await context.Users.AsNoTracking().SingleAsync();

        // Act
        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.UpdatePasswordAndRevokeSessionsAsync(user.Id, "new-hash", "new-salt", DateTimeOffset.UtcNow, CancellationToken.None));

        // Assert
        var unchanged = await context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(user.PasswordHash, unchanged.PasswordHash);
        Assert.Equal(user.PasswordSalt, unchanged.PasswordSalt);
        var refreshed = await anonymous.SendRefreshAsync(login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReturnsInvalidPasswordAndKeepsTheRefreshSessions_WhenTheCurrentPasswordIsWrong()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        var login = await anonymous.LoginWithRefreshAsync(Email, Password);
        var client = _factory.CreateBearerClient(login.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest("wrong-password", NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidPassword, error?.Code);
        var refreshed = await anonymous.SendRefreshAsync(login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_DoesNotRevokeTheSessionsOfOtherUsers()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        await anonymous.RegisterAsync("other@example.com", Password);
        var login = await anonymous.LoginWithRefreshAsync(Email, Password);
        var other = await anonymous.LoginWithRefreshAsync("other@example.com", Password);
        var client = _factory.CreateBearerClient(login.AccessToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(Password, NewPassword));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var refreshed = await anonymous.SendRefreshAsync(other.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RemovesEveryRefreshSessionOfTheUser_AndLeavesOtherUsersAlone()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        await anonymous.RegisterAsync("other@example.com", Password);
        var first = await anonymous.LoginWithRefreshAsync(Email, Password);
        var second = await anonymous.LoginWithRefreshAsync(Email, Password);
        var other = await anonymous.LoginWithRefreshAsync("other@example.com", Password);
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
        Assert.Equal(1, await CountRefreshTokensAsync());
        var refreshed = await anonymous.SendRefreshAsync(other.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsInvalidPasswordAndKeepsTheRefreshSessions_WhenThePasswordIsWrong()
    {
        // Arrange
        var anonymous = await CreateAnonymousClientWithUserAsync();
        var login = await anonymous.LoginWithRefreshAsync(Email, Password);
        var client = _factory.CreateBearerClient(login.AccessToken);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("wrong-password")),
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidPassword, error?.Code);
        var refreshed = await anonymous.SendRefreshAsync(login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    private async Task<HttpClient> CreateAnonymousClientWithUserAsync()
    {
        await _factory.EnsureDatabaseAsync();
        var anonymous = _factory.CreateCookielessClient();
        await anonymous.RegisterAsync(Email, Password);

        return anonymous;
    }

    private static async Task AssertRefreshRejectedAsync(HttpClient client, string token)
    {
        var response = await client.SendRefreshAsync(token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRefreshToken, error?.Code);
    }

    private async Task<List<DateTimeOffset?>> ReadRevocationTimesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();

        return await context.RefreshTokens.AsNoTracking().Select(token => token.RevokedAt).ToListAsync();
    }

    private async Task<int> CountRefreshTokensAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();

        return await context.RefreshTokens.CountAsync();
    }
}
