using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;
using Microsoft.Extensions.Logging;

namespace GymRatService.Service.Services;

public class CoachService(
    ICoachSuggestionsRepository suggestionsRepository,
    IWorkoutsRepository workoutsRepository,
    IWorkoutsService workoutsService,
    IExerciseCardsRepository exerciseCardsRepository,
    ISplitRepository splitRepository,
    ISessionsRepository sessionsRepository,
    ILogger<CoachService> logger) : ICoachService
{
    private const int MaximumSetChange = 1;

    private const int RationaleMaximumLength = 500;

    private static readonly TimeSpan SuggestionLifetime = TimeSpan.FromDays(7);

    public async Task<PagedResult<CoachSuggestionDto>> GetPendingSuggestionsAsync(Guid userId, CoachSuggestionFilter filter, CancellationToken cancellationToken)
    {
        await ExpireOldSuggestionsAsync(userId, cancellationToken);

        var suggestions = await suggestionsRepository.GetPendingAsync(userId, filter, cancellationToken);

        var dtos = await ToDtosAsync(userId, suggestions.Items, suggestion => suggestion.Workout?.Name ?? string.Empty, cancellationToken);

        return new PagedResult<CoachSuggestionDto>(dtos, suggestions.Page, suggestions.PageSize, suggestions.TotalCount);
    }

    public async Task<CoachSuggestionDto> ProposeExerciseChangeAsync(Guid userId, ProposeExerciseChangeRequest request, CancellationToken cancellationToken)
    {
        var workout = await GetOwnedWorkoutAsync(userId, request.WorkoutId, cancellationToken);

        var occurrences = workout.Exercises.Where(entry => entry.ExerciseId == request.ExerciseId).ToList();

        if (occurrences.Count == 0)
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionExerciseNotInWorkout, request.ExerciseId);
        }

        var current = occurrences[0];

        if (occurrences.Any(entry => !HasSameSettings(entry, current)) || !IsValidChange(request, current))
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionInvalidChange);
        }

        var now = TimeProvider.System.GetUtcNow();

        await suggestionsRepository.DismissPendingAsync(userId, workout.Id, SuggestionKind.ExerciseChange, request.ExerciseId, now, cancellationToken);

        var suggestion = new CoachSuggestion
        {
            UserId = userId,
            WorkoutId = workout.Id,
            Kind = SuggestionKind.ExerciseChange,
            ExerciseId = request.ExerciseId,
            TargetSets = request.TargetSets,
            RepsMin = request.RepsMin,
            RepsMax = request.RepsMax,
            WeightStepKg = request.WeightStepKg,
            BeforeTargetSets = current.TargetSets,
            BeforeRepsMin = current.RepsMin,
            BeforeRepsMax = current.RepsMax,
            BeforeWeightStepKg = current.WeightStepKg,
            Rationale = TrimRationale(request.Rationale),
            Status = SuggestionStatus.Pending,
            CreatedAt = now,
        };

        await suggestionsRepository.AddAsync(suggestion, cancellationToken);

        logger.LogInformation(
            "Coach proposed a change {SuggestionId} for workout {WorkoutId} exercise {ExerciseId}",
            suggestion.Id,
            suggestion.WorkoutId,
            request.ExerciseId);

        return await ToDtoAsync(userId, suggestion, workout.Name, cancellationToken);
    }

    public async Task<CoachSuggestionDto> ProposeWorkoutReplacementAsync(Guid userId, ProposeWorkoutReplacementRequest request, CancellationToken cancellationToken)
    {
        var workout = await GetOwnedWorkoutAsync(userId, request.WorkoutId, cancellationToken);

        var exerciseIds = request.Exercises.Select(entry => entry.ExerciseId).Distinct().ToList();

        if (exerciseIds.Count > 0)
        {
            var cards = await exerciseCardsRepository.GetByIdsAsync(exerciseIds, cancellationToken);

            if (cards.Count != exerciseIds.Count)
            {
                throw new BusinessLogicException(ErrorCodes.WorkoutExerciseNotFound);
            }
        }

        var before = ToSnapshots(workout);

        var after = request.Exercises
            .Select(entry => new SuggestionExerciseSnapshot(entry.ExerciseId, entry.TargetSets, entry.RepsMin, entry.RepsMax, entry.WeightStepKg))
            .ToList();

        if (IsSameAsCurrent(before, after))
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionInvalidChange);
        }

        var now = TimeProvider.System.GetUtcNow();

        await suggestionsRepository.DismissPendingAsync(userId, workout.Id, SuggestionKind.ReplaceExercises, null, now, cancellationToken);

        var suggestion = new CoachSuggestion
        {
            UserId = userId,
            WorkoutId = workout.Id,
            Kind = SuggestionKind.ReplaceExercises,
            BeforeExercises = SuggestionSnapshots.Serialize(before),
            AfterExercises = SuggestionSnapshots.Serialize(after),
            Rationale = TrimRationale(request.Rationale),
            Status = SuggestionStatus.Pending,
            CreatedAt = now,
        };

        await suggestionsRepository.AddAsync(suggestion, cancellationToken);

        logger.LogInformation("Coach proposed replacing the exercises {SuggestionId} of workout {WorkoutId}", suggestion.Id, suggestion.WorkoutId);

        return await ToDtoAsync(userId, suggestion, workout.Name, cancellationToken);
    }

    public async Task<CoachSuggestionDto> ProposeWorkoutDeletionAsync(Guid userId, ProposeWorkoutDeletionRequest request, CancellationToken cancellationToken)
    {
        var workout = await GetOwnedWorkoutAsync(userId, request.WorkoutId, cancellationToken);

        var now = TimeProvider.System.GetUtcNow();

        await suggestionsRepository.DismissPendingAsync(userId, workout.Id, SuggestionKind.DeleteWorkout, null, now, cancellationToken);

        var suggestion = new CoachSuggestion
        {
            UserId = userId,
            WorkoutId = workout.Id,
            Kind = SuggestionKind.DeleteWorkout,
            BeforeExercises = SuggestionSnapshots.Serialize(ToSnapshots(workout)),
            Rationale = TrimRationale(request.Rationale),
            Status = SuggestionStatus.Pending,
            CreatedAt = now,
        };

        await suggestionsRepository.AddAsync(suggestion, cancellationToken);

        logger.LogInformation("Coach proposed deleting workout {WorkoutId} as {SuggestionId}", suggestion.WorkoutId, suggestion.Id);

        return await ToDtoAsync(userId, suggestion, workout.Name, cancellationToken);
    }

    public async Task ApplySuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await GetPendingSuggestionAsync(userId, id, cancellationToken);

        switch (suggestion.Kind)
        {
            case SuggestionKind.ReplaceExercises:
                await ApplyReplacementAsync(userId, suggestion, cancellationToken);
                break;
            case SuggestionKind.DeleteWorkout:
                await ApplyDeletionAsync(userId, suggestion, cancellationToken);
                break;
            default:
                await ApplyExerciseChangeAsync(suggestion, cancellationToken);
                break;
        }
    }

    public async Task DismissSuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await GetPendingSuggestionAsync(userId, id, cancellationToken);

        await suggestionsRepository.SetStatusAsync([suggestion.Id], SuggestionStatus.Dismissed, TimeProvider.System.GetUtcNow(), cancellationToken);
    }

    private async Task ApplyExerciseChangeAsync(CoachSuggestion suggestion, CancellationToken cancellationToken)
    {
        var occurrences = suggestion.Workout?.Exercises.Where(entry => entry.ExerciseId == suggestion.ExerciseId).ToList() ?? [];

        if (occurrences.Count == 0)
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionExerciseNotInWorkout, suggestion.ExerciseId ?? Guid.Empty);
        }

        if (occurrences.Any(entry => entry.TargetSets != suggestion.BeforeTargetSets
            || entry.RepsMin != suggestion.BeforeRepsMin
            || entry.RepsMax != suggestion.BeforeRepsMax
            || entry.WeightStepKg != suggestion.BeforeWeightStepKg))
        {
            throw await ExpireAsStaleAsync(suggestion.Id, cancellationToken);
        }

        var current = occurrences[0];

        var settings = new WorkoutExercise
        {
            TargetSets = suggestion.TargetSets ?? current.TargetSets,
            RepsMin = suggestion.RepsMin ?? current.RepsMin,
            RepsMax = suggestion.RepsMax ?? current.RepsMax,
            WeightStepKg = suggestion.WeightStepKg ?? current.WeightStepKg,
        };

        var outcome = await suggestionsRepository.ApplyAsync(suggestion, settings, TimeProvider.System.GetUtcNow(), cancellationToken);

        switch (outcome)
        {
            case SuggestionApplyOutcome.NotPending:
                throw new BusinessLogicException(ErrorCodes.CoachSuggestionNotPending, suggestion.Id);
            case SuggestionApplyOutcome.Stale:
                throw await ExpireAsStaleAsync(suggestion.Id, cancellationToken);
        }
    }

    private async Task ApplyReplacementAsync(Guid userId, CoachSuggestion suggestion, CancellationToken cancellationToken)
    {
        await EnsureWorkoutUnchangedAsync(suggestion, cancellationToken);

        var exercises = SuggestionSnapshots.Deserialize(suggestion.AfterExercises)
            .Select(entry => new WorkoutExerciseRequest(entry.ExerciseId, entry.TargetSets, entry.RepsMin, entry.RepsMax, entry.WeightStepKg))
            .ToList();

        await workoutsService.ReplaceWorkoutExercisesAsync(userId, suggestion.WorkoutId, new ReplaceWorkoutExercisesRequest(exercises), cancellationToken);

        await suggestionsRepository.SetStatusAsync([suggestion.Id], SuggestionStatus.Applied, TimeProvider.System.GetUtcNow(), cancellationToken);
    }

    private async Task ApplyDeletionAsync(Guid userId, CoachSuggestion suggestion, CancellationToken cancellationToken)
    {
        await EnsureWorkoutUnchangedAsync(suggestion, cancellationToken);

        await workoutsService.DeleteWorkoutAsync(userId, suggestion.WorkoutId, cancellationToken);
    }

    private async Task EnsureWorkoutUnchangedAsync(CoachSuggestion suggestion, CancellationToken cancellationToken)
    {
        var workout = suggestion.Workout ?? throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, suggestion.WorkoutId);

        if (!ToSnapshots(workout).SequenceEqual(SuggestionSnapshots.Deserialize(suggestion.BeforeExercises)))
        {
            throw await ExpireAsStaleAsync(suggestion.Id, cancellationToken);
        }
    }

    private async Task<BusinessLogicException> ExpireAsStaleAsync(Guid id, CancellationToken cancellationToken)
    {
        await suggestionsRepository.SetStatusAsync([id], SuggestionStatus.Expired, TimeProvider.System.GetUtcNow(), cancellationToken);

        return new BusinessLogicException(ErrorCodes.CoachSuggestionStale, id);
    }

    private async Task<Workout> GetOwnedWorkoutAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken)
    {
        var workout = await workoutsRepository.GetByIdAsync(workoutId, cancellationToken);

        return workout is not null && workout.UserId == userId
            ? workout
            : throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, workoutId);
    }

    private async Task<CoachSuggestion> GetPendingSuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await suggestionsRepository.GetByIdAsync(id, cancellationToken);

        if (suggestion is null || suggestion.UserId != userId)
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionNotFound, id);
        }

        if (suggestion.Status != SuggestionStatus.Pending || IsExpired(suggestion, TimeProvider.System.GetUtcNow()))
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionNotPending, id);
        }

        return suggestion;
    }

    private async Task ExpireOldSuggestionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();

        var pending = await suggestionsRepository.GetPendingByUserAsync(userId, cancellationToken);

        var expired = pending.Where(suggestion => IsExpired(suggestion, now)).Select(suggestion => suggestion.Id).ToList();

        if (expired.Count > 0)
        {
            await suggestionsRepository.SetStatusAsync(expired, SuggestionStatus.Expired, now, cancellationToken);
        }
    }

    private async Task<CoachSuggestionDto> ToDtoAsync(Guid userId, CoachSuggestion suggestion, string workoutName, CancellationToken cancellationToken)
    {
        var dtos = await ToDtosAsync(userId, [suggestion], _ => workoutName, cancellationToken);

        return dtos[0];
    }

    private async Task<IReadOnlyList<CoachSuggestionDto>> ToDtosAsync(
        Guid userId,
        IReadOnlyList<CoachSuggestion> suggestions,
        Func<CoachSuggestion, string> workoutNameOf,
        CancellationToken cancellationToken)
    {
        if (suggestions.Count == 0)
        {
            return [];
        }

        var exerciseIds = suggestions.SelectMany(suggestion => suggestion.ReferencedExerciseIds()).Distinct().ToList();

        var exerciseNames = exerciseIds.Count == 0
            ? []
            : (await exerciseCardsRepository.GetByIdsAsync(exerciseIds, cancellationToken)).ToDictionary(card => card.Id, card => card.Name);

        var splitDays = suggestions.Any(suggestion => suggestion.Kind == SuggestionKind.DeleteWorkout)
            ? await splitRepository.GetByUserAsync(userId, cancellationToken)
            : [];

        var dtos = new List<CoachSuggestionDto>(suggestions.Count);

        foreach (var suggestion in suggestions)
        {
            var isDeletion = suggestion.Kind == SuggestionKind.DeleteWorkout;

            IReadOnlyList<int> scheduledDays = isDeletion
                ? [.. splitDays.Where(day => day.WorkoutId == suggestion.WorkoutId).Select(day => day.Day)]
                : [];

            var sessionCount = isDeletion
                ? await sessionsRepository.CountFinishedByWorkoutAsync(userId, suggestion.WorkoutId, cancellationToken)
                : 0;

            dtos.Add(suggestion.ToDto(workoutNameOf(suggestion), exerciseNames, scheduledDays, sessionCount));
        }

        return dtos;
    }

    private static string TrimRationale(string rationale)
    {
        var trimmed = rationale.Trim();

        return trimmed.Length > RationaleMaximumLength ? trimmed[..RationaleMaximumLength] : trimmed;
    }

    private static List<SuggestionExerciseSnapshot> ToSnapshots(Workout workout) =>
        [.. workout.Exercises
            .OrderBy(entry => entry.Position)
            .Select(entry => new SuggestionExerciseSnapshot(entry.ExerciseId, entry.TargetSets, entry.RepsMin, entry.RepsMax, entry.WeightStepKg))];

    private static bool IsSameAsCurrent(IReadOnlyList<SuggestionExerciseSnapshot> current, IReadOnlyList<SuggestionExerciseSnapshot> proposed) =>
        current.Count == proposed.Count
        && current.Zip(proposed).All(pair =>
            pair.First.ExerciseId == pair.Second.ExerciseId
            && pair.First.TargetSets == pair.Second.TargetSets
            && (pair.Second.RepsMin is null || pair.Second.RepsMin == pair.First.RepsMin)
            && (pair.Second.RepsMax is null || pair.Second.RepsMax == pair.First.RepsMax)
            && (pair.Second.WeightStepKg is null || pair.Second.WeightStepKg == pair.First.WeightStepKg));

    private static bool IsExpired(CoachSuggestion suggestion, DateTimeOffset now) =>
        now - suggestion.CreatedAt > SuggestionLifetime;

    private static bool HasSameSettings(WorkoutExercise entry, WorkoutExercise other) =>
        entry.TargetSets == other.TargetSets
        && entry.RepsMin == other.RepsMin
        && entry.RepsMax == other.RepsMax
        && entry.WeightStepKg == other.WeightStepKg;

    private static bool IsValidChange(ProposeExerciseChangeRequest request, WorkoutExercise current)
    {
        var targetSets = request.TargetSets ?? current.TargetSets;
        var repsMin = request.RepsMin ?? current.RepsMin;
        var repsMax = request.RepsMax ?? current.RepsMax;
        var weightStepKg = request.WeightStepKg ?? current.WeightStepKg;

        var changed = targetSets != current.TargetSets
            || repsMin != current.RepsMin
            || repsMax != current.RepsMax
            || weightStepKg != current.WeightStepKg;

        return changed && Math.Abs(targetSets - current.TargetSets) <= MaximumSetChange && repsMin <= repsMax;
    }
}
