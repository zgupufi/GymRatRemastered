using GymRatService.API.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace GymRatService.BLL.Tests.Validators;

public class RequestLimitsExtensionsTests
{
    [Fact]
    public void LimitRequestBodySize_CapsTheBodyAt256Kilobytes()
    {
        // Arrange
        var options = new KestrelServerOptions();

        // Act
        options.LimitRequestBodySize();

        // Assert
        Assert.Equal(262144, options.Limits.MaxRequestBodySize);
    }
}
