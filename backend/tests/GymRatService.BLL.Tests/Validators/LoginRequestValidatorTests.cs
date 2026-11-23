using GymRatService.API.Validators;
using GymRatService.Common.DTOs.Auth;

namespace GymRatService.BLL.Tests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public async Task Validate_Passes_WhenTheRequestIsValid()
    {
        // Act
        var result = await _validator.ValidateAsync(new LoginRequest("ana@example.com", "password123"));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "password123", "Email")]
    [InlineData("ana@example.com", "", "Password")]
    public async Task Validate_Fails_WhenAFieldIsEmpty(string email, string password, string expectedProperty)
    {
        // Act
        var result = await _validator.ValidateAsync(new LoginRequest(email, password));

        // Assert
        Assert.Equal(expectedProperty, Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public async Task Validate_Passes_WhenTheLengthsAreAtTheMaximum()
    {
        // Act
        var result = await _validator.ValidateAsync(new LoginRequest(new string('e', 100), new string('p', 128)));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Fails_WhenTheEmailIsLongerThanTheMaximum()
    {
        // Act
        var result = await _validator.ValidateAsync(new LoginRequest(new string('e', 101), "password123"));

        // Assert
        Assert.Equal("Email", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public async Task Validate_Fails_WhenThePasswordIsLongerThanTheMaximum()
    {
        // Act
        var result = await _validator.ValidateAsync(new LoginRequest("ana@example.com", new string('p', 129)));

        // Assert
        Assert.Equal("Password", Assert.Single(result.Errors).PropertyName);
    }
}
