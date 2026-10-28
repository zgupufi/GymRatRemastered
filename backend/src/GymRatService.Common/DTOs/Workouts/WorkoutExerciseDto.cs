using GymRatService.Common.DTOs.Exercises;

namespace GymRatService.Common.DTOs.Workouts;

public sealed record WorkoutExerciseDto(
    int Order,
    int TargetSets,
    ExerciseDto Exercise);
