using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Targets;

public sealed record ExerciseTargetDto(
    int Order,
    Guid ExerciseId,
    int Sets,
    int RepsMin,
    int RepsMax,
    decimal? WeightKg,
    int Reps,
    ProgressionReason Reason,
    decimal? LastWeightKg,
    IReadOnlyList<int> LastReps);
