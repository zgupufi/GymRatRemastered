using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Common.DTOs.Coach;

public sealed record ProposeWorkoutReplacementRequest(
    Guid WorkoutId,
    IReadOnlyList<WorkoutExerciseRequest> Exercises,
    string Rationale);
