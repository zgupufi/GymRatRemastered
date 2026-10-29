namespace GymRatService.Common.DTOs.Workouts;

public sealed record WorkoutSummaryDto(
    Guid Id,
    string Name,
    int ExerciseCount,
    IReadOnlyList<string> MainMuscles);
