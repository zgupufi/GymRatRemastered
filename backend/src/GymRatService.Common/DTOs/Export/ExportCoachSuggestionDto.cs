using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Export;

public sealed record ExportCoachSuggestionDto(
    Guid Id,
    Guid WorkoutId,
    string WorkoutName,
    Guid ExerciseId,
    string ExerciseName,
    int? TargetSets,
    int? RepsMin,
    int? RepsMax,
    decimal? WeightStepKg,
    int BeforeTargetSets,
    int BeforeRepsMin,
    int BeforeRepsMax,
    decimal BeforeWeightStepKg,
    string Rationale,
    SuggestionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
