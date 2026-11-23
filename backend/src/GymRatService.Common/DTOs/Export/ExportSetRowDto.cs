using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Export;

public sealed record ExportSetRowDto(
    Guid SessionId,
    DateTimeOffset StartedAt,
    string? WorkoutName,
    string ExerciseName,
    int SetNumber,
    int Reps,
    decimal WeightKg,
    int? RepsInReserve,
    decimal? TargetWeightKg,
    int? TargetReps,
    ProgressionReason? TargetReason);
