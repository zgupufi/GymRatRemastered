namespace GymRatService.Common.DTOs.Workouts;

public sealed record ReplaceWorkoutExercisesRequest(IReadOnlyList<WorkoutExerciseRequest> Exercises);
