using System.Globalization;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Enums;
using GymRatService.Service.Validators;

namespace GymRatService.BLL.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public async Task Validate_Passes_WhenTheRequestIsValid()
    {
        // Act
        var result = await _validator.ValidateAsync(
            new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Pounds));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ReportsEveryFailingRule()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        // Act
        var result = await _validator.ValidateAsync(
            new RegisterRequest("not-an-email", "short", "", new string('p', 51), (WeightUnit)5));

        // Assert
        Assert.Equal(
            ["Email", "Password", "FirstName", "LastName", "WeightUnit"],
            result.Errors.Select(error => error.PropertyName));
    }

    [Theory]
    [InlineData("en", "'First Name' must not be empty.")]
    [InlineData("ro", "'First Name' nu ar trebui să fie goală.")]
    public async Task Validate_UsesTheLocalizedFluentValidationMessages(string culture, string expectedMessage)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var result = await _validator.ValidateAsync(
            new RegisterRequest("ana@example.com", "password123", "", null, WeightUnit.Kilograms));

        // Assert
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedMessage, error.ErrorMessage);
    }
}
