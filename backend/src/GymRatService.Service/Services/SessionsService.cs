using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Targets;
using GymRatService.Common.Filtering;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;
using GymRatService.Service.Progression;

namespace GymRatService.Service.Services;

public class SessionsService(
    ISessionsRepository sessionsRepository,
    IWorkoutsRepository workoutsRepository,
    IExerciseCardsRepository exerciseCardsRepository) : ISessionsService
{
    private const decimal EpleyRepsDivisor = 30m;

    private const int TargetSessionCount = 2;

    private const int HistorySessionCount = 5;

    private const int StartSessionAttempts = 3;

    private static readonly TimeSpan ActiveSessionMaxAge = TimeSpan.FromHours(12);

    public async Task<StartSessionResponse> StartSessionAsync(Guid userId, StartSessionRequest request, CancellationToken cancellationToken)
    {
        await GetOwnedWorkoutAsync(userId, request.WorkoutId, cancellationToken);

        for (var attempt = 0; attempt < StartSessionAttempts; attempt++)
        {
            var now = TimeProvider.System.GetUtcNow();
            var open = await sessionsRepository.GetOpenAsync(userId, cancellationToken);

            if (open is not null)
            {
                if (open.WorkoutId == request.WorkoutId && now - open.StartedAt < ActiveSessionMaxAge)
                {
                    return new StartSessionResponse(open.Id);
                }

                await CloseOpenSessionAsync(open, now, cancellationToken);
            }

            var session = new WorkoutSession
            {
                UserId = userId,
                WorkoutId = request.WorkoutId,
                StartedAt = now,
            };

            if (await sessionsRepository.TryAddAsync(session, cancellationToken))
            {
                return new StartSessionResponse(session.Id);
            }
        }

        var existing = await sessionsRepository.GetOpenAsync(userId, cancellationToken);

        return existing is not null && existing.WorkoutId == request.WorkoutId
            ? new StartSessionResponse(existing.Id)
            : throw new InvalidOperationException("Could not start a session because another one was opened at the same time.");
    }

    private async Task CloseOpenSessionAsync(WorkoutSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (session.Sets.Count == 0)
        {
            await sessionsRepository.DeleteAsync(session.Id, cancellationToken);

            return;
        }

        var finishedAt = session.StartedAt + ActiveSessionMaxAge < now ? session.StartedAt + ActiveSessionMaxAge : now;
        var durationSeconds = (int)Math.Max(0, (finishedAt - session.StartedAt).TotalSeconds);

        await sessionsRepository.FinishAsync(session.Id, finishedAt, durationSeconds, cancellationToken);
    }

    public async Task<ActiveSessionDto> GetActiveSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var session = await sessionsRepository.GetActiveAsync(userId, cancellationToken);

        return session is not null && TimeProvider.System.GetUtcNow() - session.StartedAt < ActiveSessionMaxAge
            ? session.ToActiveSessionDto()
            : new ActiveSessionDto(null, null, null, []);
    }

    public async Task SaveExerciseSetsAsync(Guid userId, Guid id, Guid exerciseId, SaveExerciseSetsRequest request, CancellationToken cancellationToken)
    {
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
                RepsInReserve = set.RepsInReserve,
                TargetWeightKg = request.Target?.WeightKg,
                TargetReps = request.Target?.Reps,
                TargetReason = request.Target?.Reason,
            })
            .ToList();

        await sessionsRepository.ReplaceSetsAsync(session.Id, exerciseId, sets, cancellationToken);
    }

    public async Task FinishSessionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var session = await GetOpenSessionAsync(userId, id, cancellationToken);

        var finishedAt = TimeProvider.System.GetUtcNow();
        var durationSeconds = (int)Math.Max(0, (finishedAt - session.StartedAt).TotalSeconds);

        await sessionsRepository.FinishAsync(session.Id, finishedAt, durationSeconds, cancellationToken);
    }

    public async Task<LastSessionDto> GetLastSessionAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken)
    {
        await GetOwnedWorkoutAsync(userId, workoutId, cancellationToken);

        var session = await sessionsRepository.GetLastFinishedAsync(userId, workoutId, cancellationToken);

        return session?.ToLastSessionDto() ?? new LastSessionDto(null, null, null, []);
    }

    public async Task<IReadOnlyList<ExerciseProgressDto>> GetExerciseProgressAsync(Guid userId, Guid exerciseId, ProgressFilter filter, CancellationToken cancellationToken)
    {
        await EnsureExerciseExistsAsync(exerciseId, cancellationToken);

        var sets = await sessionsRepository.GetExerciseSetsAsync(userId, exerciseId, cancellationToken);

        var timeZone = string.IsNullOrEmpty(filter.TimeZone) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(filter.TimeZone);

        return sets
            .GroupBy(set => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(set.Session!.StartedAt, timeZone).DateTime))
            .OrderBy(day => day.Key)
            .Select(day =>
            {
                var best = day
                    .OrderByDescending(set => EstimateOneRepMax(set.WeightKg, set.Reps))
                    .ThenByDescending(set => set.WeightKg)
                    .ThenByDescending(set => set.Reps)
                    .First();

                return new ExerciseProgressDto(
                    day.Key,
                    new BestSetDto(best.Reps, best.WeightKg),
                    Math.Round(day.Max(set => EstimateOneRepMax(set.WeightKg, set.Reps)), 2));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ExerciseTargetDto>> GetTargetsAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken)
    {
        var workout = await GetOwnedWorkoutAsync(userId, workoutId, cancellationToken);

        var history = new Dictionary<Guid, IReadOnlyList<IReadOnlyList<LoggedSet>>>();
        var targets = new List<ExerciseTargetDto>();

        foreach (var (entry, index) in workout.Exercises.OrderBy(entry => entry.Position).Select((entry, index) => (entry, index)))
        {
            if (!history.TryGetValue(entry.ExerciseId, out var sessions))
            {
                sessions = await GetRecentSessionsAsync(userId, entry.ExerciseId, TargetSessionCount, cancellationToken);
                history[entry.ExerciseId] = sessions;
            }

            var target = ProgressionCalculator.Calculate(
                new ProgressionSettings(entry.RepsMin, entry.RepsMax, entry.WeightStepKg),
                sessions.ElementAtOrDefault(0) ?? [],
                sessions.ElementAtOrDefault(1) ?? []);

            targets.Add(new ExerciseTargetDto(
                index + 1,
                entry.ExerciseId,
                entry.TargetSets,
                entry.RepsMin,
                entry.RepsMax,
                target.WeightKg,
                target.Reps,
                target.Reason,
                target.LastWeightKg,
                target.LastReps));
        }

        return targets;
    }

    public async Task<IReadOnlyList<ExerciseHistoryDto>> GetExerciseHistoryAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken)
    {
        await EnsureExerciseExistsAsync(exerciseId, cancellationToken);

        var sets = await sessionsRepository.GetRecentExerciseSetsAsync(userId, exerciseId, HistorySessionCount, cancellationToken);

        return sets
            .GroupBy(set => set.SessionId)
            .Select(group => new ExerciseHistoryDto(
                group.First().Session!.StartedAt,
                group.OrderBy(set => set.SetNumber).Select(set => set.ToDto()).ToList()))
            .OrderByDescending(session => session.StartedAt)
            .ToList();
    }

    private async Task<IReadOnlyList<IReadOnlyList<LoggedSet>>> GetRecentSessionsAsync(Guid userId, Guid exerciseId, int sessionCount, CancellationToken cancellationToken)
    {
        var sets = await sessionsRepository.GetRecentExerciseSetsAsync(userId, exerciseId, sessionCount, cancellationToken);

        return sets
            .GroupBy(set => set.SessionId)
            .OrderByDescending(group => group.First().Session!.StartedAt)
            .Select(group => (IReadOnlyList<LoggedSet>)group.OrderBy(set => set.SetNumber).Select(set => set.ToLoggedSet()).ToList())
            .ToList();
    }

    private static decimal EstimateOneRepMax(decimal weightKg, int reps)
    {
        return reps == 1 ? weightKg : weightKg * (1 + reps / EpleyRepsDivisor);
    }

    private async Task<Workout> GetOwnedWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var workout = await workoutsRepository.GetByIdAsync(id, cancellationToken);

        return workout is not null && workout.UserId == userId
            ? workout
            : throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, id);
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
