using FluentValidation;
using GymRatService.Common.DTOs.Split;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using GymRatService.Service.Validators;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class SplitServiceTests
{
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly ISplitRepository _split = Substitute.For<ISplitRepository>();
    private readonly IWorkoutsRepository _workouts = Substitute.For<IWorkoutsRepository>();
    private readonly SplitService _service;

    public SplitServiceTests()
    {
        _service = new SplitService(_split, _workouts, new SplitDayValidator());
    }

    [Fact]
    public async Task GetSplitAsync_ReturnsSevenDaysWithRestDaysForUnassignedOnes()
    {
        // Arrange
        var chest = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest" };
        var legs = new ExerciseCard { Name = "Squat", MainMuscle = "Legs" };
        var push = new Workout
        {
            Id = Guid.CreateVersion7(),
            Name = "Push",
            Exercises =
            [
                new WorkoutExercise { Position = 1, Exercise = chest },
                new WorkoutExercise { Position = 2, Exercise = legs },
            ],
        };
        _split.GetByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(
        [
            new SplitDay { Day = 3, WorkoutId = push.Id, Workout = push },
            new SplitDay { Day = 1, WorkoutId = push.Id, Workout = push },
        ]);

        // Act
        var days = await _service.GetSplitAsync(_userId, CancellationToken.None);

        // Assert
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], days.Select(day => day.Day));
        Assert.Equal([1, 3], days.Where(day => day.Workout is not null).Select(day => day.Day));
        var monday = days[0].Workout;
        Assert.Equal(push.Id, monday?.Id);
        Assert.Equal("Push", monday?.Name);
        Assert.Equal(2, monday?.ExerciseCount);
        Assert.Equal(["Chest", "Legs"], monday?.MainMuscles);
    }

    [Fact]
    public async Task GetSplitAsync_ReturnsSevenRestDays_WhenNothingIsAssigned()
    {
        // Arrange
        _split.GetByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var days = await _service.GetSplitAsync(_userId, CancellationToken.None);

        // Assert
        Assert.Equal(7, days.Count);
        Assert.All(days, day => Assert.Null(day.Workout));
    }

    [Fact]
    public async Task AssignSplitDayAsync_StoresTheWorkout_WhenItBelongsToTheUser()
    {
        // Arrange
        var workoutId = Guid.CreateVersion7();
        _workouts.GetByIdAsync(workoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = workoutId, UserId = _userId });

        // Act
        await _service.AssignSplitDayAsync(_userId, 2, new AssignSplitDayRequest(workoutId), CancellationToken.None);

        // Assert
        await _split.Received(1).SetDayAsync(_userId, 2, workoutId, Arg.Any<CancellationToken>());
        await _split.DidNotReceiveWithAnyArgs().ClearDayAsync(default, default, default);
    }

    [Fact]
    public async Task AssignSplitDayAsync_ClearsTheDay_WhenNoWorkoutIsGiven()
    {
        // Act
        await _service.AssignSplitDayAsync(_userId, 5, new AssignSplitDayRequest(null), CancellationToken.None);

        // Assert
        await _split.Received(1).ClearDayAsync(_userId, 5, Arg.Any<CancellationToken>());
        await _split.DidNotReceiveWithAnyArgs().SetDayAsync(default, default, default, default);
    }

    [Fact]
    public async Task AssignSplitDayAsync_Throws_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        var workoutId = Guid.CreateVersion7();
        _workouts.GetByIdAsync(workoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = workoutId, UserId = Guid.CreateVersion7() });

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.AssignSplitDayAsync(_userId, 2, new AssignSplitDayRequest(workoutId), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
        await _split.DidNotReceiveWithAnyArgs().SetDayAsync(default, default, default, default);
    }

    [Fact]
    public async Task AssignSplitDayAsync_Throws_WhenTheWorkoutDoesNotExist()
    {
        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.AssignSplitDayAsync(_userId, 2, new AssignSplitDayRequest(Guid.CreateVersion7()), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task AssignSplitDayAsync_Throws_WhenTheDayIsOutsideTheWeek(int day)
    {
        // Act
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AssignSplitDayAsync(_userId, day, new AssignSplitDayRequest(null), CancellationToken.None));

        // Assert
        await _split.DidNotReceiveWithAnyArgs().ClearDayAsync(default, default, default);
    }
}
