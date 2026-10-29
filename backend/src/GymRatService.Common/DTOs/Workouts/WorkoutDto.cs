namespace GymRatService.Common.DTOs.Workouts;

public sealed record WorkoutDto(
    Guid Id,
    string Name,
    IReadOnlyList<WorkoutExerciseDto> Exercises);
