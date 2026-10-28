using GymRatService.Common.DTOs.Workouts;
using GymRatService.Service.Validators;

namespace GymRatService.BLL.Tests.Validators;

public class WorkoutValidatorsTests
{
    [Fact]
    public async Task CreateWorkoutRequestValidator_Passes_WhenTheNameIsValid()
    {
        // Act
        var result = await new CreateWorkoutRequestValidator().ValidateAsync(new CreateWorkoutRequest("Push Day"));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateWorkoutRequestValidator_Fails_WhenTheNameIsBlank(string name)
    {
        // Act
        var result = await new CreateWorkoutRequestValidator().ValidateAsync(new CreateWorkoutRequest(name));

        // Assert
        Assert.Equal("Name", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public async Task CreateWorkoutRequestValidator_Fails_WhenTheNameIsTooLong()
    {
        // Act
        var result = await new CreateWorkoutRequestValidator().ValidateAsync(new CreateWorkoutRequest(new string('w', 101)));

        // Assert
        Assert.Equal("Name", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public async Task RenameWorkoutRequestValidator_Fails_WhenTheNameIsEmptyOrTooLong()
    {
        // Arrange
        var validator = new RenameWorkoutRequestValidator();

        // Act
        var empty = await validator.ValidateAsync(new RenameWorkoutRequest(""));
        var tooLong = await validator.ValidateAsync(new RenameWorkoutRequest(new string('w', 101)));
        var valid = await validator.ValidateAsync(new RenameWorkoutRequest("Pull Day"));

        // Assert
        Assert.False(empty.IsValid);
        Assert.False(tooLong.IsValid);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task ReplaceWorkoutExercisesRequestValidator_Passes_WhenTheListIsEmptyOrValid()
    {
        // Arrange
        var validator = new ReplaceWorkoutExercisesRequestValidator();

        // Act
        var empty = await validator.ValidateAsync(new ReplaceWorkoutExercisesRequest([]));
        var valid = await validator.ValidateAsync(
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(Guid.CreateVersion7(), 3)]));

        // Assert
        Assert.True(empty.IsValid);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task ReplaceWorkoutExercisesRequestValidator_Fails_WhenTheListIsMissing()
    {
        // Act
        var result = await new ReplaceWorkoutExercisesRequestValidator().ValidateAsync(new ReplaceWorkoutExercisesRequest(null!));

        // Assert
        Assert.Equal("Exercises", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task ReplaceWorkoutExercisesRequestValidator_Fails_WhenTargetSetsAreOutOfRange(int targetSets)
    {
        // Act
        var result = await new ReplaceWorkoutExercisesRequestValidator().ValidateAsync(
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(Guid.CreateVersion7(), targetSets)]));

        // Assert
        Assert.Equal("Exercises[0].TargetSets", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public async Task ReplaceWorkoutExercisesRequestValidator_Fails_WhenTheExerciseIdIsEmpty()
    {
        // Act
        var result = await new ReplaceWorkoutExercisesRequestValidator().ValidateAsync(
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(Guid.Empty, 3)]));

        // Assert
        Assert.Equal("Exercises[0].ExerciseId", Assert.Single(result.Errors).PropertyName);
    }
}
