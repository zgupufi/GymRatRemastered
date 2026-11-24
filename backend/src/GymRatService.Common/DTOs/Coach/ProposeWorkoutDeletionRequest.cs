namespace GymRatService.Common.DTOs.Coach;

public sealed record ProposeWorkoutDeletionRequest(Guid WorkoutId, string Rationale);
