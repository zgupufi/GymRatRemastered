using GymRatService.API.Validators;
using GymRatService.Common.DTOs.Users;

namespace GymRatService.BLL.Tests.Validators;

public class AccountRequestValidatorsTests
{
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();
    private readonly DeleteAccountRequestValidator _deleteAccountValidator = new();

    [Fact]
    public async Task ChangePassword_Passes_WhenTheRequestIsValid()
    {
        // Act
        var result = await _changePasswordValidator.ValidateAsync(new ChangePasswordRequest("password123", "new-password"));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "new-password", "CurrentPassword")]
    [InlineData("password123", "", "NewPassword")]
    [InlineData("password123", "short", "NewPassword")]
    [InlineData("password123", "password123", "NewPassword")]
    public async Task ChangePassword_Fails_WhenARuleIsBroken(string current, string next, string expectedProperty)
    {
        // Act
        var result = await _changePasswordValidator.ValidateAsync(new ChangePasswordRequest(current, next));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == expectedProperty);
    }

    [Fact]
    public async Task DeleteAccount_Passes_WhenThePasswordIsGiven()
    {
        // Act
        var result = await _deleteAccountValidator.ValidateAsync(new DeleteAccountRequest("password123"));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task DeleteAccount_Fails_WhenThePasswordIsEmpty()
    {
        // Act
        var result = await _deleteAccountValidator.ValidateAsync(new DeleteAccountRequest(""));

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Password", Assert.Single(result.Errors).PropertyName);
    }
}
