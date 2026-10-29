using FluentValidation;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using GymRatService.Service.Validators;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class WorkoutsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly IWorkoutsRepository _workouts = Substitute.For<IWorkoutsRepository>();
    private readonly IExerciseCardsRepository _exercises = Substitute.For<IExerciseCardsRepository>();
    private readonly WorkoutsService _service;

    public WorkoutsServiceTests()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        _service = new WorkoutsService(
            _workouts,
            _exercises,
            new WorkoutFilterValidator(),
            new CreateWorkoutRequestValidator(),
            new RenameWorkoutRequestValidator(),
            new ReplaceWorkoutExercisesRequestValidator(),
            timeProvider);
    }

    [Fact]
    public async Task GetWorkoutSummariesAsync_ReturnsTheCountAndDistinctMainMusclesInOrder()
    {
        // Arrange
        var chest = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest" };
        var legs = new ExerciseCard { Name = "Squat", MainMuscle = "Legs" };
        var workout = new Workout
        {
            Name = "Push",
            UserId = _userId,
            Exercises =
            [
                new WorkoutExercise { Position = 2, TargetSets = 3, Exercise = legs },
                new WorkoutExercise { Position = 1, TargetSets = 3, Exercise = chest },
                new WorkoutExercise { Position = 3, TargetSets = 3, Exercise = chest },
            ],
        };
        var filter = new WorkoutFilter();
        _workouts.GetByUserAsync(_userId, filter, Arg.Any<CancellationToken>()).Returns(new PagedResult<Workout>([workout], 1, 20, 1));

        // Act
        var summaries = await _service.GetWorkoutSummariesAsync(_userId, filter, CancellationToken.None);

        // Assert
        Assert.Equal(1, summaries.TotalCount);
        var summary = Assert.Single(summaries.Items);
        Assert.Equal(workout.Id, summary.Id);
        Assert.Equal("Push", summary.Name);
        Assert.Equal(3, summary.ExerciseCount);
        Assert.Equal(["Chest", "Legs"], summary.MainMuscles);
    }

    [Fact]
    public async Task GetWorkoutSummariesAsync_ThrowsAValidationException_WhenThePageIsZero()
    {
        // Arrange
        var filter = new WorkoutFilter { Pagination = new PaginationFilter { Page = 0 } };

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetWorkoutSummariesAsync(_userId, filter, CancellationToken.None));

        // Assert
        Assert.Equal("Pagination.Page", Assert.Single(exception.Errors).PropertyName);
    }

    [Fact]
    public async Task GetWorkoutAsync_ReturnsTheExercisesInOrderWithTargetSets()
    {
        // Arrange
        var chest = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest" };
        var legs = new ExerciseCard { Name = "Squat", MainMuscle = "Legs" };
        var workout = new Workout
        {
            Name = "Push",
            UserId = _userId,
            Exercises =
            [
                new WorkoutExercise { Position = 2, TargetSets = 5, Exercise = legs },
                new WorkoutExercise { Position = 1, TargetSets = 4, Exercise = chest },
            ],
        };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        var result = await _service.GetWorkoutAsync(_userId, workout.Id, CancellationToken.None);

        // Assert
        Assert.Equal(workout.Id, result.Id);
        Assert.Equal("Push", result.Name);
        Assert.Equal(
            [
                new WorkoutExerciseDto(1, 4, new ExerciseDto(chest.Id, "Bench Press", "Chest", null, null, null, null, null)),
                new WorkoutExerciseDto(2, 5, new ExerciseDto(legs.Id, "Squat", "Legs", null, null, null, null, null)),
            ],
            result.Exercises);
    }

    [Fact]
    public async Task GetWorkoutAsync_ThrowsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = Guid.CreateVersion7() };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetWorkoutAsync(_userId, workout.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
        Assert.Equal(workout.Id, Assert.Single(exception.Arguments));
    }

    [Fact]
    public async Task GetWorkoutAsync_ThrowsWorkoutNotFound_WhenTheWorkoutIsMissing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workouts.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Workout?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetWorkoutAsync(_userId, id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
    }

    [Fact]
    public async Task CreateWorkoutAsync_StoresTheTrimmedNameForTheUserAndReturnsTheId()
    {
        // Arrange
        Workout? stored = null;
        await _workouts.AddAsync(Arg.Do<Workout>(workout =>
        {
            workout.Id = Guid.CreateVersion7();
            stored = workout;
        }), Arg.Any<CancellationToken>());

        // Act
        var response = await _service.CreateWorkoutAsync(_userId, new CreateWorkoutRequest(" Push Day "), CancellationToken.None);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal("Push Day", stored.Name);
        Assert.Equal(_userId, stored.UserId);
        Assert.Equal(Now, stored.CreatedAt);
        Assert.Equal(stored.Id, response.Id);
    }

    [Fact]
    public async Task CreateWorkoutAsync_ThrowsAValidationException_WhenTheNameIsEmpty()
    {
        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateWorkoutAsync(_userId, new CreateWorkoutRequest(""), CancellationToken.None));

        // Assert
        Assert.Equal("Name", Assert.Single(exception.Errors).PropertyName);
        await _workouts.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RenameWorkoutAsync_RenamesTheOwnedWorkoutWithTheTrimmedName()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = _userId };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        await _service.RenameWorkoutAsync(_userId, workout.Id, new RenameWorkoutRequest(" Pull "), CancellationToken.None);

        // Assert
        await _workouts.Received(1).RenameAsync(workout.Id, "Pull", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameWorkoutAsync_ThrowsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = Guid.CreateVersion7() };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.RenameWorkoutAsync(_userId, workout.Id, new RenameWorkoutRequest("Pull"), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
        await _workouts.DidNotReceiveWithAnyArgs().RenameAsync(default, default!, default);
    }

    [Fact]
    public async Task ReplaceWorkoutExercisesAsync_StoresTheEntriesInRequestOrder()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = _userId };
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);
        _exercises.CountExistingAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(2);
        IReadOnlyList<WorkoutExercise>? stored = null;
        await _workouts.ReplaceExercisesAsync(
            workout.Id,
            Arg.Do<IReadOnlyList<WorkoutExercise>>(entries => stored = entries),
            Arg.Any<CancellationToken>());
        var request = new ReplaceWorkoutExercisesRequest(
        [
            new WorkoutExerciseRequest(first, 4),
            new WorkoutExerciseRequest(second, 3),
            new WorkoutExerciseRequest(first, 2),
        ]);

        // Act
        await _service.ReplaceWorkoutExercisesAsync(_userId, workout.Id, request, CancellationToken.None);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal([first, second, first], stored.Select(entry => entry.ExerciseId));
        Assert.Equal([1, 2, 3], stored.Select(entry => entry.Position));
        Assert.Equal([4, 3, 2], stored.Select(entry => entry.TargetSets));
        Assert.All(stored, entry => Assert.Equal(workout.Id, entry.WorkoutId));
    }

    [Fact]
    public async Task ReplaceWorkoutExercisesAsync_ThrowsExerciseNotFound_WhenAnExerciseDoesNotExist()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = _userId };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);
        _exercises.CountExistingAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(0);
        var request = new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(Guid.CreateVersion7(), 3)]);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ReplaceWorkoutExercisesAsync(_userId, workout.Id, request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutExerciseNotFound, exception.Code);
        await _workouts.DidNotReceiveWithAnyArgs().ReplaceExercisesAsync(default, default!, default);
    }

    [Fact]
    public async Task ReplaceWorkoutExercisesAsync_ThrowsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = Guid.CreateVersion7() };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.ReplaceWorkoutExercisesAsync(_userId, workout.Id, new ReplaceWorkoutExercisesRequest([]), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
    }

    [Fact]
    public async Task DeleteWorkoutAsync_DeletesTheOwnedWorkout()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = _userId };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        await _service.DeleteWorkoutAsync(_userId, workout.Id, CancellationToken.None);

        // Assert
        await _workouts.Received(1).DeleteAsync(workout.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteWorkoutAsync_ThrowsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        var workout = new Workout { Name = "Push", UserId = Guid.CreateVersion7() };
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.DeleteWorkoutAsync(_userId, workout.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
        await _workouts.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }
}
