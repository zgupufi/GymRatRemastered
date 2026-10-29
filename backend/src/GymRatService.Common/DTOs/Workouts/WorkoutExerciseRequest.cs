namespace GymRatService.Common.DTOs.Workouts;

public sealed record WorkoutExerciseRequest(Guid ExerciseId, int TargetSets);
