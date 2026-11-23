namespace GymRatService.Common.DTOs.Export;

public sealed record ExportSessionDto(
    Guid Id,
    Guid? WorkoutId,
    string? WorkoutName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? DurationSeconds,
    IReadOnlyList<ExportSetDto> Sets);
