namespace GymRatService.Common.DTOs.Export;

public sealed record ExportWorkoutExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    int Position,
    int TargetSets,
    int RepsMin,
    int RepsMax,
    decimal WeightStepKg);
