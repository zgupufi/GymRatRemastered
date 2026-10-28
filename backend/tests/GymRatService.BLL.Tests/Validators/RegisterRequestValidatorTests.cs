using System.Globalization;
using FluentValidation;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Service;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.BLL.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly IValidator<RegisterRequest> _validator = new ServiceCollection()
        .AddLogging()
        .AddServiceLayer(connectionString: null)
        .BuildServiceProvider()
        .GetRequiredService<IValidator<RegisterRequest>>();

    [Fact]
    public async Task Validate_Passes_WhenTheRequestIsValid()
    {
        // Act
        var result = await _validator.ValidateAsync(
            new RegisterRequest(" Ana@Example.com ", "password123", "Ana", null, WeightUnit.Pounds));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("en", "The first name is required.")]
    [InlineData("ro", "Prenumele este obligatoriu.")]
    public async Task Validate_ReturnsALocalizedErrorWithItsCode(string culture, string expectedMessage)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var result = await _validator.ValidateAsync(
            new RegisterRequest("ana@example.com", "password123", "", null, WeightUnit.Kilograms));

        // Assert
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.FirstNameRequired, error.ErrorCode);
        Assert.Equal(expectedMessage, error.ErrorMessage);
    }

    [Fact]
    public async Task Validate_StopsAtTheFirstFailingRule()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        // Act
        var result = await _validator.ValidateAsync(
            new RegisterRequest("not-an-email", "short", "", null, (WeightUnit)5));

        // Assert
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.InvalidEmail, error.ErrorCode);
    }
}
