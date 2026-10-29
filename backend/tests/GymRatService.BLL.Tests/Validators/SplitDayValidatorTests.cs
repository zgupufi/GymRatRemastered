using GymRatService.Service.Validators;

namespace GymRatService.BLL.Tests.Validators;

public class SplitDayValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public async Task SplitDayValidator_Passes_WhenTheDayIsBetweenOneAndSeven(int day)
    {
        // Act
        var result = await new SplitDayValidator().ValidateAsync(day);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-3)]
    public async Task SplitDayValidator_Fails_WhenTheDayIsOutsideTheWeek(int day)
    {
        // Act
        var result = await new SplitDayValidator().ValidateAsync(day);

        // Assert
        Assert.False(result.IsValid);
    }
}
