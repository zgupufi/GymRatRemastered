using GymRatService.API.Validators;
using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;

namespace GymRatService.BLL.Tests.Validators;

public class SmartTrainingValidatorsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public async Task SessionSetRequestValidator_AcceptsRepsInReserveFromZeroToThree(int? repsInReserve, bool valid)
    {
        // Act
        var result = await new SessionSetRequestValidator().ValidateAsync(new SessionSetRequest(8, 60, repsInReserve));

        // Assert
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task SaveExerciseSetsRequestValidator_ValidatesTheServedTargetWhenPresent()
    {
        // Arrange
        var validator = new SaveExerciseSetsRequestValidator();

        // Act
        var none = await validator.ValidateAsync(new SaveExerciseSetsRequest([]));
        var valid = await validator.ValidateAsync(new SaveExerciseSetsRequest([], new ServedTargetRequest(62.5m, 8, ProgressionReason.IncreaseWeight)));
        var invalid = await validator.ValidateAsync(new SaveExerciseSetsRequest([], new ServedTargetRequest(-1, 0, (ProgressionReason)99)));

        // Assert
        Assert.True(none.IsValid);
        Assert.True(valid.IsValid);
        Assert.Equal(["Target.Reason", "Target.Reps", "Target.WeightKg"], invalid.Errors.Select(error => error.PropertyName).Order());
    }

    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData(6, 10, 1.25, true)]
    [InlineData(1, 30, 5.0, true)]
    [InlineData(0, 10, null, false)]
    [InlineData(8, 31, null, false)]
    [InlineData(10, 8, null, false)]
    [InlineData(8, null, null, false)]
    [InlineData(null, 12, null, false)]
    [InlineData(null, null, 3.0, false)]
    public async Task WorkoutExerciseRequestValidator_ChecksTheRepRangeAndStep(int? repsMin, int? repsMax, double? step, bool valid)
    {
        // Act
        var result = await new WorkoutExerciseRequestValidator()
            .ValidateAsync(new WorkoutExerciseRequest(Guid.CreateVersion7(), 3, repsMin, repsMax, (decimal?)step));

        // Assert
        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(3, null, null, null, "Why", true)]
    [InlineData(null, 6, 10, 2.0, "Why", true)]
    [InlineData(null, null, null, null, "Why", false)]
    [InlineData(0, null, null, null, "Why", false)]
    [InlineData(null, 0, null, null, "Why", false)]
    [InlineData(null, null, 31, null, "Why", false)]
    [InlineData(null, null, null, 0.5, "Why", false)]
    [InlineData(3, null, null, null, "", false)]
    public async Task ProposeExerciseChangeRequestValidator_RequiresAValidChangeAndARationale(int? sets, int? repsMin, int? repsMax, double? step, string rationale, bool valid)
    {
        // Act
        var result = await new ProposeExerciseChangeRequestValidator()
            .ValidateAsync(new ProposeExerciseChangeRequest(Guid.CreateVersion7(), Guid.CreateVersion7(), sets, repsMin, repsMax, (decimal?)step, rationale));

        // Assert
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task ProposeExerciseChangeRequestValidator_RequiresBothIds()
    {
        // Act
        var result = await new ProposeExerciseChangeRequestValidator()
            .ValidateAsync(new ProposeExerciseChangeRequest(Guid.Empty, Guid.Empty, 3, null, null, null, "Why"));

        // Assert
        Assert.Equal(["ExerciseId", "WorkoutId"], result.Errors.Select(error => error.PropertyName).Order());
    }

    [Fact]
    public async Task CoachSuggestionFilterValidator_ChecksPagination()
    {
        // Arrange
        var validator = new CoachSuggestionFilterValidator();

        // Act
        var valid = await validator.ValidateAsync(new CoachSuggestionFilter());
        var invalid = await validator.ValidateAsync(new CoachSuggestionFilter { Pagination = new PaginationFilter { Page = 0 } });

        // Assert
        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
    }
}
