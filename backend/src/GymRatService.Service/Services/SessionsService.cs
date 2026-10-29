using FluentValidation;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;

namespace GymRatService.Service.Services;

public class SessionsService(
    ISessionsRepository sessionsRepository,
    IWorkoutsRepository workoutsRepository,
    IExerciseCardsRepository exerciseCardsRepository,
    IValidator<StartSessionRequest> startValidator,
    IValidator<SaveExerciseSetsRequest> saveSetsValidator,
    TimeProvider timeProvider) : ISessionsService
{
    private const decimal EpleyRepsDivisor = 30m;

    public async Task<StartSessionResponse> StartSessionAsync(Guid userId, StartSessionRequest request, CancellationToken cancellationToken)
    {
        await startValidator.ValidateAndThrowAsync(request, cancellationToken);

        await EnsureWorkoutOwnedAsync(userId, request.WorkoutId, cancellationToken);

        var session = new WorkoutSession
        {
            UserId = userId,
            WorkoutId = request.WorkoutId,
            StartedAt = timeProvider.GetUtcNow(),
        };

        await sessionsRepository.AddAsync(session, cancellationToken);

        return new StartSessionResponse(session.Id);
    }

    public async Task SaveExerciseSetsAsync(Guid userId, Guid id, Guid exerciseId, SaveExerciseSetsRequest request, CancellationToken cancellationToken)
    {
        await saveSetsValidator.ValidateAndThrowAsync(request, cancellationToken);

        var session = await GetOpenSessionAsync(userId, id, cancellationToken);

        await EnsureExerciseExistsAsync(exerciseId, cancellationToken);

        var sets = request.Sets
            .Select((set, index) => new SessionSet
            {
                SessionId = session.Id,
                ExerciseId = exerciseId,
                SetNumber = index + 1,
                Reps = set.Reps,
                WeightKg = set.WeightKg,
            })
            .ToList();

        await sessionsRepository.ReplaceSetsAsync(session.Id, exerciseId, sets, cancellationToken);
    }

    public async Task FinishSessionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var session = await GetOpenSessionAsync(userId, id, cancellationToken);

        var finishedAt = timeProvider.GetUtcNow();
        var durationSeconds = (int)Math.Max(0, (finishedAt - session.StartedAt).TotalSeconds);

        await sessionsRepository.FinishAsync(session.Id, finishedAt, durationSeconds, cancellationToken);
    }

    public async Task<LastSessionDto> GetLastSessionAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken)
    {
        await EnsureWorkoutOwnedAsync(userId, workoutId, cancellationToken);

        var session = await sessionsRepository.GetLastFinishedAsync(userId, workoutId, cancellationToken);

        return session?.ToLastSessionDto() ?? new LastSessionDto(null, null, null, []);
    }

    public async Task<IReadOnlyList<ExerciseProgressDto>> GetExerciseProgressAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken)
    {
        await EnsureExerciseExistsAsync(exerciseId, cancellationToken);

        var sets = await sessionsRepository.GetExerciseSetsAsync(userId, exerciseId, cancellationToken);

        return sets
            .GroupBy(set => DateOnly.FromDateTime(set.Session!.StartedAt.UtcDateTime))
            .OrderBy(day => day.Key)
            .Select(day =>
            {
                var best = day.OrderByDescending(set => set.WeightKg).ThenByDescending(set => set.Reps).First();

                return new ExerciseProgressDto(
                    day.Key,
                    new BestSetDto(best.Reps, best.WeightKg),
                    Math.Round(day.Max(set => EstimateOneRepMax(set.WeightKg, set.Reps)), 2));
            })
            .ToList();
    }

    private static decimal EstimateOneRepMax(decimal weightKg, int reps)
    {
        return reps == 1 ? weightKg : weightKg * (1 + reps / EpleyRepsDivisor);
    }

    private async Task EnsureWorkoutOwnedAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var workout = await workoutsRepository.GetByIdAsync(id, cancellationToken);

        if (workout is null || workout.UserId != userId)
        {
            throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, id);
        }
    }

    private async Task<WorkoutSession> GetOpenSessionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var session = await sessionsRepository.GetByIdAsync(id, cancellationToken);

        if (session is null || session.UserId != userId)
        {
            throw new BusinessLogicException(ErrorCodes.SessionNotFound, id);
        }

        return session.FinishedAt is null
            ? session
            : throw new BusinessLogicException(ErrorCodes.SessionAlreadyFinished, id);
    }

    private async Task EnsureExerciseExistsAsync(Guid exerciseId, CancellationToken cancellationToken)
    {
        if (await exerciseCardsRepository.GetByIdAsync(exerciseId, cancellationToken) is null)
        {
            throw new BusinessLogicException(ErrorCodes.ExerciseNotFound, exerciseId);
        }
    }
}
