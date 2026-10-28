using System.Globalization;
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

    [Theory]
    [InlineData("en", "The access token does not identify a user.")]
    [InlineData("ro", "Tokenul de acces nu identifică un utilizator.")]
    public void GetUserId_ThrowsALocalizedInvalidToken_WhenTheSubjectIsMissing(string culture, string expectedMessage)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var exception = Assert.Throws<ServiceException>(() => principal.GetUserId());

        // Assert
        Assert.Equal(ErrorCodes.InvalidToken, exception.Code);
        Assert.Equal(expectedMessage, exception.Message);
    }
}
