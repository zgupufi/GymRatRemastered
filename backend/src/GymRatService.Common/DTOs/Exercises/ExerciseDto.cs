using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Exercises;

public sealed record ExerciseDto(
    Guid Id,
    string Name,
    MuscleGroup MainMuscle,
    IReadOnlyList<Muscle> SecondaryMuscles,
    Equipment Equipment,
    Difficulty Difficulty,
    MovementPattern MovementPattern,
    string? Description,
    string? Setup,
    string? Execution,
    string? YoutubeLink);
