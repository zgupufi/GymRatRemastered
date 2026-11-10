using GymRatService.Common.DTOs.Coach;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class CoachMappings
{
    public static CoachSuggestionDto ToDto(this CoachSuggestion suggestion) => new(
        suggestion.Id,
        suggestion.WorkoutId,
        suggestion.Workout?.Name ?? string.Empty,
        suggestion.ExerciseId,
        suggestion.Exercise?.Name ?? string.Empty,
        suggestion.TargetSets,
        suggestion.RepsMin,
        suggestion.RepsMax,
        suggestion.WeightStepKg,
        suggestion.BeforeTargetSets,
        suggestion.BeforeRepsMin,
        suggestion.BeforeRepsMax,
        suggestion.BeforeWeightStepKg,
        suggestion.Rationale,
        suggestion.Status);
}
