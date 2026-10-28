using System.Text;
using GymRatService.Common.Settings;
using GymRatService.Repository.Entities;
using GymRatService.Service.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace GymRatService.BLL.Tests.Security;

public class JwtTokenGeneratorTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough";

    private readonly JwtSettings _settings = new()
    {
        Key = Key,
        Issuer = "GymRatService",
        Audience = "GymRatClient",
        ExpiryMinutes = 30,
    };

    [Fact]
    public async Task Generate_IssuesASignedTokenForTheUser()
    {
        // Arrange
        var user = new User { Email = "ana@example.com" };
        var generator = new JwtTokenGenerator(Options.Create(_settings), TimeProvider.System);

        // Act
        var response = generator.Generate(user);

        // Assert
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "GymRatService",
            ValidAudience = "GymRatClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        });
        Assert.True(result.IsValid);
        var token = (JsonWebToken)result.SecurityToken;
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("ana@example.com", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public void Generate_ExpiresAfterTheConfiguredMinutes()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        var generator = new JwtTokenGenerator(Options.Create(_settings), timeProvider);

        // Act
        var response = generator.Generate(new User { Email = "ana@example.com" });

        // Assert
        Assert.Equal(now.AddMinutes(30), response.ExpiresAt);
        Assert.Equal(now.AddMinutes(30).UtcDateTime, new JsonWebToken(response.AccessToken).ValidTo);
    }

    [Fact]
    public void Generate_Throws_WhenTheKeyIsNotConfigured()
    {
        // Arrange
        _settings.Key = string.Empty;
        var generator = new JwtTokenGenerator(Options.Create(_settings), TimeProvider.System);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => generator.Generate(new User()));

        // Assert
        Assert.Equal("JwtSettings:Key is not configured.", exception.Message);
    }
}
