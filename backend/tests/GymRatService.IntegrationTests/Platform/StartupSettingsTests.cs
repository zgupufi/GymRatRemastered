using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace GymRatService.IntegrationTests.Platform;

public sealed class StartupSettingsTests
{
    private const string JwtKeySetting = "JwtSettings:Key";

    private const string PepperSetting = "PasswordSettings:Pepper";

    private const string ConnectionStringSetting = "ConnectionStrings:DefaultConnection";

    private const string FirstAllowedOriginSetting = "Cors:AllowedOrigins:0";

    private const string AllowedOriginsSetting = "Cors:AllowedOrigins";

    private const string SameOriginSetting = "Cors:SameOrigin";

    public static TheoryData<string, string> RejectedSettings => new()
    {
        { JwtKeySetting, string.Empty },
        { JwtKeySetting, "only-thirty-one-bytes-long-key!" },
        { JwtKeySetting, "gymrat-development-only-jwt-signing-key" },
        { JwtKeySetting, "change-me-to-a-random-string-of-at-least-32-characters" },
        { JwtKeySetting, "integration-test-signing-key-that-is-long-enough" },
        { JwtKeySetting, "  GYMRAT-development-only-jwt-signing-key  " },
        { PepperSetting, string.Empty },
        { PepperSetting, "   " },
        { PepperSetting, "gymrat-development-only-password-pepper" },
        { PepperSetting, "change-me-to-a-different-random-string" },
        { PepperSetting, "integration-test-pepper" },
        { ConnectionStringSetting, string.Empty },
        { ConnectionStringSetting, "   " },
    };

    [Fact]
    public async Task Start_Succeeds_InProductionWithGeneratedSecretsAndAnAllowedOrigin()
    {
        // Arrange
        using var factory = CreateProductionFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Start_Succeeds_InProductionWithoutOrigins_WhenTheWebAppIsServedFromTheSameOrigin()
    {
        // Arrange
        using var factory = CreateProductionFactory();
        factory.Settings[FirstAllowedOriginSetting] = string.Empty;
        factory.Settings[SameOriginSetting] = "true";
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RejectedSettings))]
    public void Start_Fails_InProduction_WhenASettingIsMissingOrADevelopmentValue(string setting, string value)
    {
        // Arrange
        using var factory = CreateProductionFactory();
        factory.Settings[setting] = value;

        // Act
        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        // Assert
        var failure = Assert.Single(exception.Failures);
        Assert.Contains(setting, failure);

        if (!string.IsNullOrWhiteSpace(value))
        {
            Assert.DoesNotContain(value.Trim(), failure, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Start_Fails_InProduction_WhenNoOriginIsAllowedAndTheWebAppIsHostedElsewhere()
    {
        // Arrange
        using var factory = CreateProductionFactory();
        factory.Settings[FirstAllowedOriginSetting] = string.Empty;

        // Act
        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        // Assert
        var failure = Assert.Single(exception.Failures);
        Assert.Contains(AllowedOriginsSetting, failure);
        Assert.Contains(SameOriginSetting, failure);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task Start_Succeeds_OutsideProduction_WithDevelopmentSecretsAndNoConnectionString(string environment)
    {
        // Arrange
        using var factory = new GymRatApiFactory(environment);
        factory.Settings[JwtKeySetting] = "gymrat-development-only-jwt-signing-key";
        factory.Settings[PepperSetting] = "gymrat-development-only-password-pepper";
        factory.Settings[ConnectionStringSetting] = string.Empty;
        factory.Settings[FirstAllowedOriginSetting] = string.Empty;
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static GymRatApiFactory CreateProductionFactory()
    {
        var factory = new GymRatApiFactory(environment: "Production");
        factory.Settings[JwtKeySetting] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        factory.Settings[PepperSetting] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        factory.Settings[ConnectionStringSetting] = "Host=database;Database=gymrat";
        factory.Settings[FirstAllowedOriginSetting] = "https://app.example.com";

        return factory;
    }
}
