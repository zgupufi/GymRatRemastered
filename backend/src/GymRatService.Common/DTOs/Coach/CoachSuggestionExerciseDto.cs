using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Coach;

public sealed record CoachSuggestionExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    int TargetSets,
    int? PreviousTargetSets,
    SuggestionExerciseChange Change);
