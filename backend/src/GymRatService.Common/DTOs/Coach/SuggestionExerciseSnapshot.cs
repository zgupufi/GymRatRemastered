namespace GymRatService.Common.DTOs.Coach;

public sealed record SuggestionExerciseSnapshot(
    Guid ExerciseId,
    int TargetSets,
    int? RepsMin,
    int? RepsMax,
    decimal? WeightStepKg);
