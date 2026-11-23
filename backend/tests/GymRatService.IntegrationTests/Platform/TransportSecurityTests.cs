using System.Net;
using System.Security.Cryptography;

namespace GymRatService.IntegrationTests.Platform;

public sealed class TransportSecurityTests
{
    private const string HstsHeader = "Strict-Transport-Security";

    private const string ProxyAddress = "10.0.0.5";

    [Fact]
    public async Task GetExercises_SendsHsts_InProductionOverHttps()
    {
        // Arrange
        using var factory = CreateProductionFactory();
        var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://api.example.com");

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("max-age=15552000", Assert.Single(response.Headers.GetValues(HstsHeader)));
    }

    [Fact]
    public async Task GetExercises_OmitsHsts_InProductionOverPlainHttp()
    {
        // Arrange
        using var factory = CreateProductionFactory();
        var client = factory.CreateClient();
        client.BaseAddress = new Uri("http://api.example.com");

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.False(response.Headers.Contains(HstsHeader));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task GetExercises_OmitsHsts_WhenTheEnvironmentAllowsDevelopmentSettings(string environment)
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment);
        var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://api.example.com");

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.False(response.Headers.Contains(HstsHeader));
    }

    [Fact]
    public async Task GetExercises_SendsHsts_WhenATrustedProxyForwardsAnHttpsRequest()
    {
        // Arrange
        using var factory = CreateProductionFactory();
        factory.Settings["ForwardedHeaders:TrustedNetworks:0"] = $"{ProxyAddress}/32";
        var client = factory.CreateClientWithIp(ProxyAddress);
        client.BaseAddress = new Uri("http://api.example.com");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal("max-age=15552000", Assert.Single(response.Headers.GetValues(HstsHeader)));
    }

    [Theory]
    [InlineData("10.0.0.5/32", "203.0.113.9")]
    [InlineData(null, ProxyAddress)]
    public async Task GetExercises_IgnoresTheForwardedProtocol_WhenTheRequestDoesNotComeFromATrustedProxy(string? trustedNetwork, string senderAddress)
    {
        // Arrange
        using var factory = CreateProductionFactory();

        if (trustedNetwork is not null)
        {
            factory.Settings["ForwardedHeaders:TrustedNetworks:0"] = trustedNetwork;
        }

        var client = factory.CreateClientWithIp(senderAddress);
        client.BaseAddress = new Uri("http://api.example.com");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.False(response.Headers.Contains(HstsHeader));
    }

    private static GymRatApiFactory CreateProductionFactory()
    {
        var factory = new GymRatApiFactory(environment: "Production");
        factory.Settings["JwtSettings:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        factory.Settings["PasswordSettings:Pepper"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        factory.Settings["ConnectionStrings:DefaultConnection"] = "Host=database;Database=gymrat";
        factory.Settings["Cors:AllowedOrigins:0"] = "https://app.example.com";

        return factory;
    }
}
