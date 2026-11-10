namespace GymRatService.Common.DTOs.Coach;

public sealed record ProposeExerciseChangeRequest(
    Guid WorkoutId,
    Guid ExerciseId,
    int? TargetSets,
    int? RepsMin,
    int? RepsMax,
    decimal? WeightStepKg,
    string Rationale);
