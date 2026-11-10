namespace GymRatService.Common.DTOs.Sessions;

public sealed record ExerciseHistoryDto(DateTimeOffset StartedAt, IReadOnlyList<SessionSetDto> Sets);
