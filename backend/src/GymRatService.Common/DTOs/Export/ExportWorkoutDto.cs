namespace GymRatService.Common.DTOs.Export;

public sealed record ExportWorkoutDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ExportWorkoutExerciseDto> Exercises);
