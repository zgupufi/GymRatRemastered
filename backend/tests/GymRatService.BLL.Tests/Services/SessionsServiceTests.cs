using FluentValidation;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using GymRatService.Service.Validators;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class SessionsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _workoutId = Guid.CreateVersion7();
    private readonly Guid _exerciseId = Guid.CreateVersion7();
    private readonly ISessionsRepository _sessions = Substitute.For<ISessionsRepository>();
    private readonly IWorkoutsRepository _workouts = Substitute.For<IWorkoutsRepository>();
    private readonly IExerciseCardsRepository _exercises = Substitute.For<IExerciseCardsRepository>();
    private readonly SessionsService _service;

    public SessionsServiceTests()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        _service = new SessionsService(
            _sessions,
            _workouts,
            _exercises,
            new StartSessionRequestValidator(),
            new SaveExerciseSetsRequestValidator(),
            timeProvider);
    }

    [Fact]
    public async Task StartSessionAsync_AddsASessionStartedNowForTheCallersWorkout()
    {
        // Arrange
        _workouts.GetByIdAsync(_workoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = _workoutId, UserId = _userId });

        // Act
        await _service.StartSessionAsync(_userId, new StartSessionRequest(_workoutId), CancellationToken.None);

        // Assert
        await _sessions.Received(1).AddAsync(
            Arg.Is<WorkoutSession>(session => session.UserId == _userId && session.WorkoutId == _workoutId && session.StartedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartSessionAsync_ThrowsAValidationException_WhenTheWorkoutIdIsEmpty()
    {
        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.StartSessionAsync(_userId, new StartSessionRequest(Guid.Empty), CancellationToken.None));

        // Assert
        Assert.Equal("WorkoutId", Assert.Single(exception.Errors).PropertyName);
    }

    [Fact]
    public async Task StartSessionAsync_ThrowsWorkoutNotFound_WhenTheWorkoutIsMissingOrBelongsToAnotherUser()
    {
        // Arrange
        var otherWorkoutId = Guid.CreateVersion7();
        _workouts.GetByIdAsync(otherWorkoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = otherWorkoutId, UserId = Guid.CreateVersion7() });

        // Act
        var missing = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.StartSessionAsync(_userId, new StartSessionRequest(_workoutId), CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.StartSessionAsync(_userId, new StartSessionRequest(otherWorkoutId), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, missing.Code);
        Assert.Equal(ErrorCodes.WorkoutNotFound, foreign.Code);
        await _sessions.DidNotReceive().AddAsync(Arg.Any<WorkoutSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveExerciseSetsAsync_ReplacesTheSetsWithConsecutiveSetNumbers()
    {
        // Arrange
        var session = ArrangeOpenSession();
        var request = new SaveExerciseSetsRequest([new SessionSetRequest(8, 60), new SessionSetRequest(6, 62.5m)]);

        // Act
        await _service.SaveExerciseSetsAsync(_userId, session.Id, _exerciseId, request, CancellationToken.None);

        // Assert
        await _sessions.Received(1).ReplaceSetsAsync(
            session.Id,
            _exerciseId,
            Arg.Is<IReadOnlyList<SessionSet>>(sets =>
                sets.Count == 2
                && sets[0].SetNumber == 1 && sets[0].Reps == 8 && sets[0].WeightKg == 60 && sets[0].SessionId == session.Id && sets[0].ExerciseId == _exerciseId
                && sets[1].SetNumber == 2 && sets[1].Reps == 6 && sets[1].WeightKg == 62.5m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveExerciseSetsAsync_ThrowsAValidationException_WhenASetIsInvalid()
    {
        // Arrange
        var session = ArrangeOpenSession();

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SaveExerciseSetsAsync(_userId, session.Id, _exerciseId, new SaveExerciseSetsRequest([new SessionSetRequest(0, 60)]), CancellationToken.None));

        // Assert
        Assert.Equal("Sets[0].Reps", Assert.Single(exception.Errors).PropertyName);
    }

    [Fact]
    public async Task SaveExerciseSetsAsync_ThrowsSessionNotFound_WhenTheSessionIsMissingOrBelongsToAnotherUser()
    {
        // Arrange
        var foreign = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = Guid.CreateVersion7(), StartedAt = Now };
        _sessions.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);
        var request = new SaveExerciseSetsRequest([]);

        // Act
        var missing = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.SaveExerciseSetsAsync(_userId, Guid.CreateVersion7(), _exerciseId, request, CancellationToken.None));
        var other = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.SaveExerciseSetsAsync(_userId, foreign.Id, _exerciseId, request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.SessionNotFound, missing.Code);
        Assert.Equal(ErrorCodes.SessionNotFound, other.Code);
    }

    [Fact]
    public async Task SaveExerciseSetsAsync_ThrowsSessionAlreadyFinished_WhenTheSessionIsClosed()
    {
        // Arrange
        var session = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = _userId, StartedAt = Now.AddHours(-1), FinishedAt = Now };
        _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.SaveExerciseSetsAsync(_userId, session.Id, _exerciseId, new SaveExerciseSetsRequest([]), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.SessionAlreadyFinished, exception.Code);
    }

    [Fact]
    public async Task SaveExerciseSetsAsync_ThrowsExerciseNotFound_WhenTheExerciseDoesNotExist()
    {
        // Arrange
        var session = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = _userId, StartedAt = Now };
        _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.SaveExerciseSetsAsync(_userId, session.Id, _exerciseId, new SaveExerciseSetsRequest([]), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ExerciseNotFound, exception.Code);
        await _sessions.DidNotReceive().ReplaceSetsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<SessionSet>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishSessionAsync_StoresTheElapsedSecondsAsTheDuration()
    {
        // Arrange
        var session = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = _userId, StartedAt = Now.AddMinutes(-45).AddSeconds(-30) };
        _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        await _service.FinishSessionAsync(_userId, session.Id, CancellationToken.None);

        // Assert
        await _sessions.Received(1).FinishAsync(session.Id, Now, 2730, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishSessionAsync_StoresZeroSeconds_WhenTheSessionStartedAfterNow()
    {
        // Arrange
        var session = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = _userId, StartedAt = Now.AddMinutes(5) };
        _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        await _service.FinishSessionAsync(_userId, session.Id, CancellationToken.None);

        // Assert
        await _sessions.Received(1).FinishAsync(session.Id, Now, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishSessionAsync_ThrowsSessionAlreadyFinished_WhenTheSessionIsClosed()
    {
        // Arrange
        var session = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = _userId, StartedAt = Now.AddHours(-1), FinishedAt = Now };
        _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.FinishSessionAsync(_userId, session.Id, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.SessionAlreadyFinished, exception.Code);
        await _sessions.DidNotReceive().FinishAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLastSessionAsync_ReturnsAnEmptySession_WhenThereIsNoFinishedSession()
    {
        // Arrange
        _workouts.GetByIdAsync(_workoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = _workoutId, UserId = _userId });

        // Act
        var last = await _service.GetLastSessionAsync(_userId, _workoutId, CancellationToken.None);

        // Assert
        Assert.Null(last.SessionId);
        Assert.Null(last.StartedAt);
        Assert.Null(last.DurationSeconds);
        Assert.Empty(last.Exercises);
    }

    [Fact]
    public async Task GetLastSessionAsync_GroupsTheSetsPerExerciseOrderedBySetNumber()
    {
        // Arrange
        _workouts.GetByIdAsync(_workoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = _workoutId, UserId = _userId });
        var squatId = Guid.CreateVersion7();
        var session = new WorkoutSession
        {
            Id = Guid.CreateVersion7(),
            UserId = _userId,
            WorkoutId = _workoutId,
            StartedAt = Now.AddHours(-1),
            DurationSeconds = 3600,
            Sets =
            [
                new SessionSet { ExerciseId = _exerciseId, SetNumber = 2, Reps = 6, WeightKg = 62.5m },
                new SessionSet { ExerciseId = squatId, SetNumber = 1, Reps = 5, WeightKg = 100 },
                new SessionSet { ExerciseId = _exerciseId, SetNumber = 1, Reps = 8, WeightKg = 60 },
            ],
        };
        _sessions.GetLastFinishedAsync(_userId, _workoutId, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var last = await _service.GetLastSessionAsync(_userId, _workoutId, CancellationToken.None);

        // Assert
        Assert.Equal(session.Id, last.SessionId);
        Assert.Equal(Now.AddHours(-1), last.StartedAt);
        Assert.Equal(3600, last.DurationSeconds);
        Assert.Equal([_exerciseId, squatId], last.Exercises.Select(exercise => exercise.ExerciseId));
        Assert.Equal([new SessionSetDto(1, 8, 60), new SessionSetDto(2, 6, 62.5m)], last.Exercises[0].Sets);
        Assert.Equal([new SessionSetDto(1, 5, 100)], last.Exercises[1].Sets);
    }

    [Fact]
    public async Task GetLastSessionAsync_ThrowsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        _workouts.GetByIdAsync(_workoutId, Arg.Any<CancellationToken>()).Returns(new Workout { Id = _workoutId, UserId = Guid.CreateVersion7() });

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetLastSessionAsync(_userId, _workoutId, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.WorkoutNotFound, exception.Code);
    }

    [Fact]
    public async Task GetExerciseProgressAsync_ReturnsTheBestSetAndEpleyEstimatePerDayInDateOrder()
    {
        // Arrange
        _exercises.GetByIdAsync(_exerciseId, Arg.Any<CancellationToken>()).Returns(new ExerciseCard { Id = _exerciseId });
        var day1 = new WorkoutSession { StartedAt = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero) };
        var day2 = new WorkoutSession { StartedAt = new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero) };
        var sets = new List<SessionSet>
        {
            new() { Session = day2, Reps = 1, WeightKg = 105 },
            new() { Session = day1, Reps = 10, WeightKg = 80 },
            new() { Session = day1, Reps = 5, WeightKg = 100 },
            new() { Session = day1, Reps = 3, WeightKg = 100 },
        };
        _sessions.GetExerciseSetsAsync(_userId, _exerciseId, Arg.Any<CancellationToken>()).Returns(sets);

        // Act
        var progress = await _service.GetExerciseProgressAsync(_userId, _exerciseId, CancellationToken.None);

        // Assert
        Assert.Equal(2, progress.Count);
        Assert.Equal(new DateOnly(2026, 10, 20), progress[0].Date);
        Assert.Equal(new BestSetDto(5, 100), progress[0].BestSet);
        Assert.Equal(116.67m, progress[0].EstimatedOneRepMax);
        Assert.Equal(new DateOnly(2026, 10, 22), progress[1].Date);
        Assert.Equal(new BestSetDto(1, 105), progress[1].BestSet);
        Assert.Equal(105m, progress[1].EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgressAsync_ReturnsAnEmptyList_WhenThereAreNoSets()
    {
        // Arrange
        _exercises.GetByIdAsync(_exerciseId, Arg.Any<CancellationToken>()).Returns(new ExerciseCard { Id = _exerciseId });
        _sessions.GetExerciseSetsAsync(_userId, _exerciseId, Arg.Any<CancellationToken>()).Returns(new List<SessionSet>());

        // Act
        var progress = await _service.GetExerciseProgressAsync(_userId, _exerciseId, CancellationToken.None);

        // Assert
        Assert.Empty(progress);
    }

    [Fact]
    public async Task GetExerciseProgressAsync_ThrowsExerciseNotFound_WhenTheExerciseDoesNotExist()
    {
        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetExerciseProgressAsync(_userId, _exerciseId, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ExerciseNotFound, exception.Code);
    }

    private WorkoutSession ArrangeOpenSession()
    {
        var session = new WorkoutSession { Id = Guid.CreateVersion7(), UserId = _userId, StartedAt = Now };
        _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _exercises.GetByIdAsync(_exerciseId, Arg.Any<CancellationToken>()).Returns(new ExerciseCard { Id = _exerciseId });

        return session;
    }
}
