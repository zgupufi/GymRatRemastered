using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class CoachServiceTests
{
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _exerciseId = Guid.CreateVersion7();
    private readonly ICoachSuggestionsRepository _suggestions = Substitute.For<ICoachSuggestionsRepository>();
    private readonly IWorkoutsRepository _workouts = Substitute.For<IWorkoutsRepository>();
    private readonly CoachService _service;

    public CoachServiceTests()
    {
        _service = new CoachService(_suggestions, _workouts, NullLogger<CoachService>.Instance);
    }

    [Fact]
    public async Task ProposeExerciseChangeAsync_StoresAPendingSuggestionWithTheCurrentValuesAndDismissesTheOldOne()
    {
        // Arrange
        var workout = CreateWorkout(Entry(4, 8, 12, 2.5m));
        CoachSuggestion? stored = null;
        await _suggestions.AddAsync(Arg.Do<CoachSuggestion>(suggestion => stored = suggestion), Arg.Any<CancellationToken>());
        var request = new ProposeExerciseChangeRequest(workout.Id, _exerciseId, 3, 6, 10, null, "  Stalled three times.  ");

        // Act
        var result = await _service.ProposeExerciseChangeAsync(_userId, request, CancellationToken.None);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal((4, 8, 12, 2.5m), (stored.BeforeTargetSets, stored.BeforeRepsMin, stored.BeforeRepsMax, stored.BeforeWeightStepKg));
        Assert.Equal((3, 6, 10, (decimal?)null), (stored.TargetSets, stored.RepsMin, stored.RepsMax, stored.WeightStepKg));
        Assert.Equal(SuggestionStatus.Pending, stored.Status);
        Assert.Equal("Stalled three times.", result.Rationale);
        Assert.Equal(("Push", "Bench Press"), (result.WorkoutName, result.ExerciseName));
        await _suggestions.Received(1).DismissPendingAsync(_userId, workout.Id, _exerciseId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProposeExerciseChangeAsync_TrimsTheRationaleToFiveHundredCharacters()
    {
        // Arrange
        var workout = CreateWorkout(Entry(4, 8, 12, 2.5m));
        var request = new ProposeExerciseChangeRequest(workout.Id, _exerciseId, null, null, null, 5m, new string('a', 600));

        // Act
        var result = await _service.ProposeExerciseChangeAsync(_userId, request, CancellationToken.None);

        // Assert
        Assert.Equal(500, result.Rationale.Length);
    }

    [Theory]
    [InlineData(6, null, null, null)]
    [InlineData(2, null, null, null)]
    [InlineData(null, 13, null, null)]
    [InlineData(4, 8, 12, 2.5)]
    public async Task ProposeExerciseChangeAsync_ThrowsInvalidChange_WhenTheChangeIsOutOfBoundsOrEmpty(int? sets, int? repsMin, int? repsMax, double? step)
    {
        // Arrange
        var workout = CreateWorkout(Entry(4, 8, 12, 2.5m));
        var request = new ProposeExerciseChangeRequest(workout.Id, _exerciseId, sets, repsMin, repsMax, (decimal?)step, "Why");

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ProposeExerciseChangeAsync(_userId, request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionInvalidChange, exception.Code);
        await _suggestions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ProposeExerciseChangeAsync_ThrowsInvalidChange_WhenDuplicateOccurrencesDiffer()
    {
        // Arrange
        var workout = CreateWorkout(Entry(4, 8, 12, 2.5m), Entry(3, 8, 12, 2.5m));
        var request = new ProposeExerciseChangeRequest(workout.Id, _exerciseId, 3, null, null, null, "Why");

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ProposeExerciseChangeAsync(_userId, request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionInvalidChange, exception.Code);
    }

    [Fact]
    public async Task ProposeExerciseChangeAsync_ThrowsWorkoutNotFound_ForAnotherUsersWorkout()
    {
        // Arrange
        var workout = CreateWorkout(Entry(4, 8, 12, 2.5m));
        workout.UserId = Guid.CreateVersion7();
        var request = new ProposeExerciseChangeRequest(workout.Id, _exerciseId, 3, null, null, null, "Why");

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ProposeExerciseChangeAsync(_userId, request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
    }

    [Fact]
    public async Task ProposeExerciseChangeAsync_ThrowsExerciseNotInWorkout_WhenTheExerciseIsMissing()
    {
        // Arrange
        var workout = CreateWorkout(Entry(4, 8, 12, 2.5m));
        var request = new ProposeExerciseChangeRequest(workout.Id, Guid.CreateVersion7(), 3, null, null, null, "Why");

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ProposeExerciseChangeAsync(_userId, request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionExerciseNotInWorkout, exception.Code);
    }

    [Fact]
    public async Task ApplySuggestionAsync_AppliesTheMergedValues()
    {
        // Arrange
        var suggestion = CreateSuggestion(Entry(4, 8, 12, 2.5m));
        WorkoutExercise? applied = null;
        await _suggestions.ApplyAsync(suggestion, Arg.Do<WorkoutExercise>(settings => applied = settings), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        // Act
        await _service.ApplySuggestionAsync(_userId, suggestion.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(applied);
        Assert.Equal((3, 8, 12, 2.5m), (applied.TargetSets, applied.RepsMin, applied.RepsMax, applied.WeightStepKg));
    }

    [Theory]
    [InlineData(SuggestionApplyOutcome.NotPending, ErrorCodes.CoachSuggestionNotPending)]
    [InlineData(SuggestionApplyOutcome.Stale, ErrorCodes.CoachSuggestionStale)]
    public async Task ApplySuggestionAsync_Throws_WhenTheRepositoryLosesARace(SuggestionApplyOutcome outcome, string code)
    {
        // Arrange
        var suggestion = CreateSuggestion(Entry(4, 8, 12, 2.5m));
        _suggestions.ApplyAsync(suggestion, Arg.Any<WorkoutExercise>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(outcome);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ApplySuggestionAsync(_userId, suggestion.Id, CancellationToken.None));

        // Assert
        Assert.Equal(code, exception.Code);
    }

    [Fact]
    public async Task ApplySuggestionAsync_ThrowsStale_WhenAnyOccurrenceChanged()
    {
        // Arrange
        var suggestion = CreateSuggestion(Entry(4, 8, 12, 2.5m), Entry(4, 8, 10, 2.5m));

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ApplySuggestionAsync(_userId, suggestion.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionStale, exception.Code);
        await _suggestions.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task ApplySuggestionAsync_ThrowsExerciseNotInWorkout_WhenTheExerciseWasRemoved()
    {
        // Arrange
        var suggestion = CreateSuggestion();

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ApplySuggestionAsync(_userId, suggestion.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionExerciseNotInWorkout, exception.Code);
    }

    [Fact]
    public async Task ApplySuggestionAsync_ThrowsNotPending_WhenTheSuggestionExpired()
    {
        // Arrange
        var suggestion = CreateSuggestion(Entry(4, 8, 12, 2.5m));
        suggestion.CreatedAt = DateTimeOffset.UtcNow.AddDays(-8);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ApplySuggestionAsync(_userId, suggestion.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionNotPending, exception.Code);
    }

    [Fact]
    public async Task DismissSuggestionAsync_ThrowsNotFound_ForAnotherUsersSuggestion()
    {
        // Arrange
        var suggestion = CreateSuggestion(Entry(4, 8, 12, 2.5m));
        suggestion.UserId = Guid.CreateVersion7();

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.DismissSuggestionAsync(_userId, suggestion.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.CoachSuggestionNotFound, exception.Code);
    }

    [Fact]
    public async Task GetPendingSuggestionsAsync_ExpiresSuggestionsOlderThanSevenDaysBeforeListing()
    {
        // Arrange
        var fresh = new CoachSuggestion { Id = Guid.CreateVersion7(), UserId = _userId, CreatedAt = DateTimeOffset.UtcNow.AddDays(-6) };
        var old = new CoachSuggestion { Id = Guid.CreateVersion7(), UserId = _userId, CreatedAt = DateTimeOffset.UtcNow.AddDays(-8) };
        _suggestions.GetPendingByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns([fresh, old]);
        var filter = new CoachSuggestionFilter();
        _suggestions.GetPendingAsync(_userId, filter, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CoachSuggestion>([fresh], 1, 20, 1));

        // Act
        var result = await _service.GetPendingSuggestionsAsync(_userId, filter, CancellationToken.None);

        // Assert
        Assert.Equal(fresh.Id, Assert.Single(result.Items).Id);
        await _suggestions.Received(1).SetStatusAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == old.Id),
            SuggestionStatus.Expired,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPendingSuggestionsAsync_DoesNotExpireAnything_WhenAllAreFresh()
    {
        // Arrange
        _suggestions.GetPendingByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);
        var filter = new CoachSuggestionFilter();
        _suggestions.GetPendingAsync(_userId, filter, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CoachSuggestion>([], 1, 20, 0));

        // Act
        await _service.GetPendingSuggestionsAsync(_userId, filter, CancellationToken.None);

        // Assert
        await _suggestions.DidNotReceiveWithAnyArgs().SetStatusAsync(default!, default, default, default);
    }

    private WorkoutExercise Entry(int sets, int repsMin, int repsMax, decimal step) => new()
    {
        ExerciseId = _exerciseId,
        TargetSets = sets,
        RepsMin = repsMin,
        RepsMax = repsMax,
        WeightStepKg = step,
        Exercise = new ExerciseCard { Id = _exerciseId, Name = "Bench Press", MainMuscle = "Chest" },
    };

    private Workout CreateWorkout(params WorkoutExercise[] entries)
    {
        var workout = new Workout { Id = Guid.CreateVersion7(), UserId = _userId, Name = "Push", Exercises = [.. entries] };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        return workout;
    }

    private CoachSuggestion CreateSuggestion(params WorkoutExercise[] entries)
    {
        var suggestion = new CoachSuggestion
        {
            Id = Guid.CreateVersion7(),
            UserId = _userId,
            ExerciseId = _exerciseId,
            TargetSets = 3,
            BeforeTargetSets = 4,
            BeforeRepsMin = 8,
            BeforeRepsMax = 12,
            BeforeWeightStepKg = 2.5m,
            Status = SuggestionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Workout = new Workout { Name = "Push", Exercises = [.. entries] },
        };
        _suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        return suggestion;
    }
}
