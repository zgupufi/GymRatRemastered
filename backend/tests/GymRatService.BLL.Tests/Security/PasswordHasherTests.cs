using GymRatService.Common.Settings;
using GymRatService.Service.Security;
using Microsoft.Extensions.Options;

namespace GymRatService.BLL.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = CreateHasher("test-pepper");

    [Fact]
    public void Hash_ProducesAHashThatVerifiesWithTheSamePassword()
    {
        // Act
        var hashed = _hasher.Hash("correct horse battery");

        // Assert
        Assert.NotEqual("correct horse battery", hashed.Hash);
        Assert.Equal(32, Convert.FromBase64String(hashed.Hash).Length);
        Assert.Equal(16, Convert.FromBase64String(hashed.Salt).Length);
        Assert.True(_hasher.Verify("correct horse battery", hashed.Hash, hashed.Salt));
    }

    [Fact]
    public void Hash_UsesARandomSaltPerCall()
    {
        // Act
        var first = _hasher.Hash("same-password");
        var second = _hasher.Hash("same-password");

        // Assert
        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenThePasswordIsWrong()
    {
        // Arrange
        var hashed = _hasher.Hash("right-password");

        // Act
        var verified = _hasher.Verify("wrong-password", hashed.Hash, hashed.Salt);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenThePepperChanged()
    {
        // Arrange
        var hashed = _hasher.Hash("right-password");
        var otherHasher = CreateHasher("another-pepper");

        // Act
        var verified = otherHasher.Verify("right-password", hashed.Hash, hashed.Salt);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void Hash_Throws_WhenThePepperIsNotConfigured()
    {
        // Arrange
        var hasher = CreateHasher(string.Empty);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => hasher.Hash("any-password"));

        // Assert
        Assert.Equal("PasswordSettings:Pepper is not configured.", exception.Message);
    }

    private static PasswordHasher CreateHasher(string pepper) =>
        new(Options.Create(new PasswordSettings { Pepper = pepper }));
}
