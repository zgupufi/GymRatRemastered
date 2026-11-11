namespace GymRatService.Common.DTOs.Sessions;

public sealed record ActiveSessionDto(
    Guid? SessionId,
    Guid? WorkoutId,
    DateTimeOffset? StartedAt,
    IReadOnlyList<LastSessionExerciseDto> Exercises);
