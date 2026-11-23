using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Export;

public sealed record ExportSetDto(
    Guid ExerciseId,
    string ExerciseName,
    int SetNumber,
    int Reps,
    decimal WeightKg,
    int? RepsInReserve,
    decimal? TargetWeightKg,
    int? TargetReps,
    ProgressionReason? TargetReason);
