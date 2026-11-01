namespace GymRatService.Common.DTOs.Sessions;

public sealed record LastSessionDto(
    Guid? SessionId,
    DateTimeOffset? StartedAt,
    int? DurationSeconds,
    IReadOnlyList<LastSessionExerciseDto> Exercises);
