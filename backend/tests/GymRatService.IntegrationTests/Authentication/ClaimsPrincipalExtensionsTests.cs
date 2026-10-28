using System.Security.Claims;
using GymRatService.API.Extensions;
using GymRatService.Common.Errors;
using GymRatService.Service.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.IntegrationTests.Authentication;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_ReturnsTheSubjectClaim()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]));

        // Act
        var result = principal.GetUserId();

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public void GetUserId_ThrowsUnauthorizedAccess_WhenTheSubjectIsMissing()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var exception = Assert.Throws<BusinessLogicException>(() => principal.GetUserId());

        // Assert
        Assert.Equal(ErrorCodes.UnauthorizedAccess, exception.Code);
    }
}
