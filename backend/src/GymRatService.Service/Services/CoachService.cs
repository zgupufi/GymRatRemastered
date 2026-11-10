using GymRatService.Common.DTOs.Coach;
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
    ILogger<CoachService> logger) : ICoachService
{
    private const int MaximumSetChange = 1;

    private const int RationaleMaximumLength = 500;

    private static readonly TimeSpan SuggestionLifetime = TimeSpan.FromDays(7);

    public async Task<PagedResult<CoachSuggestionDto>> GetPendingSuggestionsAsync(Guid userId, CoachSuggestionFilter filter, CancellationToken cancellationToken)
    {
        await ExpireOldSuggestionsAsync(userId, cancellationToken);

        var suggestions = await suggestionsRepository.GetPendingAsync(userId, filter, cancellationToken);

        return suggestions.Map(suggestion => suggestion.ToDto());
    }

    public async Task<CoachSuggestionDto> ProposeExerciseChangeAsync(Guid userId, ProposeExerciseChangeRequest request, CancellationToken cancellationToken)
    {
        var workout = await workoutsRepository.GetByIdAsync(request.WorkoutId, cancellationToken);

        if (workout is null || workout.UserId != userId)
        {
            throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, request.WorkoutId);
        }

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

        await suggestionsRepository.DismissPendingAsync(userId, workout.Id, request.ExerciseId, now, cancellationToken);

        var rationale = request.Rationale.Trim();

        var suggestion = new CoachSuggestion
        {
            UserId = userId,
            WorkoutId = workout.Id,
            ExerciseId = request.ExerciseId,
            TargetSets = request.TargetSets,
            RepsMin = request.RepsMin,
            RepsMax = request.RepsMax,
            WeightStepKg = request.WeightStepKg,
            BeforeTargetSets = current.TargetSets,
            BeforeRepsMin = current.RepsMin,
            BeforeRepsMax = current.RepsMax,
            BeforeWeightStepKg = current.WeightStepKg,
            Rationale = rationale.Length > RationaleMaximumLength ? rationale[..RationaleMaximumLength] : rationale,
            Status = SuggestionStatus.Pending,
            CreatedAt = now,
        };

        await suggestionsRepository.AddAsync(suggestion, cancellationToken);

        logger.LogInformation(
            "Coach proposed a change {SuggestionId} for workout {WorkoutId} exercise {ExerciseId}",
            suggestion.Id,
            suggestion.WorkoutId,
            suggestion.ExerciseId);

        return suggestion.ToDto() with { WorkoutName = workout.Name, ExerciseName = current.Exercise?.Name ?? string.Empty };
    }

    public async Task ApplySuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await GetPendingSuggestionAsync(userId, id, cancellationToken);

        var occurrences = suggestion.Workout?.Exercises.Where(entry => entry.ExerciseId == suggestion.ExerciseId).ToList() ?? [];

        if (occurrences.Count == 0)
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionExerciseNotInWorkout, suggestion.ExerciseId);
        }

        if (occurrences.Any(entry => entry.TargetSets != suggestion.BeforeTargetSets
            || entry.RepsMin != suggestion.BeforeRepsMin
            || entry.RepsMax != suggestion.BeforeRepsMax
            || entry.WeightStepKg != suggestion.BeforeWeightStepKg))
        {
            throw new BusinessLogicException(ErrorCodes.CoachSuggestionStale, id);
        }

        var settings = new WorkoutExercise
        {
            TargetSets = suggestion.TargetSets ?? suggestion.BeforeTargetSets,
            RepsMin = suggestion.RepsMin ?? suggestion.BeforeRepsMin,
            RepsMax = suggestion.RepsMax ?? suggestion.BeforeRepsMax,
            WeightStepKg = suggestion.WeightStepKg ?? suggestion.BeforeWeightStepKg,
        };

        await suggestionsRepository.ApplyAsync(suggestion, settings, TimeProvider.System.GetUtcNow(), cancellationToken);
    }

    public async Task DismissSuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await GetPendingSuggestionAsync(userId, id, cancellationToken);

        await suggestionsRepository.SetStatusAsync([suggestion.Id], SuggestionStatus.Dismissed, TimeProvider.System.GetUtcNow(), cancellationToken);
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
