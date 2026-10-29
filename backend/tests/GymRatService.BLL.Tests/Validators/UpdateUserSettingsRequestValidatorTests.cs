using System.Globalization;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Enums;
using GymRatService.Service.Validators;

namespace GymRatService.BLL.Tests.Validators;

public class UpdateUserSettingsRequestValidatorTests
{
    private readonly UpdateUserSettingsRequestValidator _validator = new();

    [Theory]
    [InlineData(WeightUnit.Pounds, 30)]
    [InlineData(WeightUnit.Kilograms, 600)]
    [InlineData(null, 120)]
    [InlineData(WeightUnit.Pounds, null)]
    [InlineData(null, null)]
    public async Task Validate_Passes_WhenTheRequestIsValid(WeightUnit? weightUnit, int? defaultRestTimeSeconds)
    {
        // Act
        var result = await _validator.ValidateAsync(new UpdateUserSettingsRequest(weightUnit, defaultRestTimeSeconds));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(601)]
    public async Task Validate_Fails_WhenTheRestTimeIsOutOfRange(int defaultRestTimeSeconds)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        // Act
        var result = await _validator.ValidateAsync(new UpdateUserSettingsRequest(null, defaultRestTimeSeconds));

        // Assert
        var error = Assert.Single(result.Errors);
        Assert.Equal("DefaultRestTimeSeconds", error.PropertyName);
        Assert.Equal($"'Default Rest Time Seconds' must be between 30 and 600. You entered {defaultRestTimeSeconds}.", error.ErrorMessage);
    }

    [Fact]
    public async Task Validate_ReportsEveryFailingRule()
    {
        // Act
        var result = await _validator.ValidateAsync(new UpdateUserSettingsRequest((WeightUnit)5, 0));

        // Assert
        Assert.Equal(
            ["WeightUnit", "DefaultRestTimeSeconds"],
            result.Errors.Select(error => error.PropertyName));
    }
}
