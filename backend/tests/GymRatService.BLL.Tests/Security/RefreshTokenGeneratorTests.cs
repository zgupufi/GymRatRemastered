using GymRatService.Service.Security;

namespace GymRatService.BLL.Tests.Security;

public class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsAnUnpredictableTokenAndItsHash()
    {
        // Act
        var first = _generator.Generate();
        var second = _generator.Generate();

        // Assert
        Assert.Equal(64, first.Token.Length);
        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Token, first.Hash);
        Assert.Equal(first.Hash, _generator.Hash(first.Token));
    }

    [Fact]
    public void Hash_IsStableAndDiffersPerToken()
    {
        // Assert
        Assert.Equal(_generator.Hash("token"), _generator.Hash("token"));
        Assert.NotEqual(_generator.Hash("token"), _generator.Hash("other-token"));
        Assert.Equal(64, _generator.Hash("token").Length);
    }
}
