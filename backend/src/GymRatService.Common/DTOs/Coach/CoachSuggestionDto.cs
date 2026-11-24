using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Coach;

public sealed record CoachSuggestionDto(
    Guid Id,
    SuggestionKind Kind,
    Guid WorkoutId,
    string WorkoutName,
    Guid? ExerciseId,
    string ExerciseName,
    int? TargetSets,
    int? RepsMin,
    int? RepsMax,
    decimal? WeightStepKg,
    int? CurrentTargetSets,
    int? CurrentRepsMin,
    int? CurrentRepsMax,
    decimal? CurrentWeightStepKg,
    IReadOnlyList<CoachSuggestionExerciseDto> Exercises,
    IReadOnlyList<int> ScheduledDays,
    int SessionCount,
    string Rationale,
    SuggestionStatus Status);
