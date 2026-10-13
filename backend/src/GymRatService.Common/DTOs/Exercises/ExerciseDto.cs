namespace GymRatService.Common.DTOs.Exercises;

public sealed record ExerciseDto(
    Guid Id,
    string Name,
    string MainMuscle,
    string? SubMuscles,
    string? Equipment,
    string? Difficulty,
    string? Description,
    string? YoutubeLink);
