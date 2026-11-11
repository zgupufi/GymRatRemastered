namespace GymRatService.Common.DTOs.Workouts;

public sealed record WorkoutExerciseRequest(Guid ExerciseId, int TargetSets, int? RepsMin = null, int? RepsMax = null, decimal? WeightStepKg = null);
