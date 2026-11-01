using GymRatService.Common.DTOs.Sessions;
using GymRatService.Service.Validators;

namespace GymRatService.BLL.Tests.Validators;

public class SessionValidatorsTests
{
    [Fact]
    public async Task StartSessionRequestValidator_FailsOnlyWhenTheWorkoutIdIsEmpty()
    {
        // Arrange
        var validator = new StartSessionRequestValidator();

        // Act
        var empty = await validator.ValidateAsync(new StartSessionRequest(Guid.Empty));
        var valid = await validator.ValidateAsync(new StartSessionRequest(Guid.CreateVersion7()));

        // Assert
        Assert.Equal("WorkoutId", Assert.Single(empty.Errors).PropertyName);
        Assert.True(valid.IsValid);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1001, 50)]
    [InlineData(5, -1)]
    [InlineData(5, 2000.5)]
    [InlineData(5, 20.123)]
    public async Task SessionSetRequestValidator_Fails_WhenTheRepsOrWeightAreOutOfRange(int reps, double weightKg)
    {
        // Act
        var result = await new SessionSetRequestValidator().ValidateAsync(new SessionSetRequest(reps, (decimal)weightKg));

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1000, 2000)]
    [InlineData(8, 62.5)]
    public async Task SessionSetRequestValidator_Passes_WhenTheRepsAndWeightAreInRange(int reps, double weightKg)
    {
        // Act
        var result = await new SessionSetRequestValidator().ValidateAsync(new SessionSetRequest(reps, (decimal)weightKg));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SaveExerciseSetsRequestValidator_Passes_WhenTheSetsAreEmptyOrValid()
    {
        // Arrange
        var validator = new SaveExerciseSetsRequestValidator();

        // Act
        var empty = await validator.ValidateAsync(new SaveExerciseSetsRequest([]));
        var valid = await validator.ValidateAsync(new SaveExerciseSetsRequest([new SessionSetRequest(8, 60)]));

        // Assert
        Assert.True(empty.IsValid);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task SaveExerciseSetsRequestValidator_Fails_WhenTheSetsAreNullTooManyOrInvalid()
    {
        // Arrange
        var validator = new SaveExerciseSetsRequestValidator();
        var tooMany = Enumerable.Range(0, 51).Select(_ => new SessionSetRequest(5, 50)).ToList();

        // Act
        var missing = await validator.ValidateAsync(new SaveExerciseSetsRequest(null!));
        var overflowing = await validator.ValidateAsync(new SaveExerciseSetsRequest(tooMany));
        var invalid = await validator.ValidateAsync(new SaveExerciseSetsRequest([new SessionSetRequest(0, 50)]));

        // Assert
        Assert.False(missing.IsValid);
        Assert.False(overflowing.IsValid);
        Assert.Equal("Sets[0].Reps", Assert.Single(invalid.Errors).PropertyName);
    }
}
