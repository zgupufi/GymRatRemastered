using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.Enums;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class CoachMappings
{
    public static CoachSuggestionDto ToDto(
        this CoachSuggestion suggestion,
        string workoutName,
        IReadOnlyDictionary<Guid, string> exerciseNames,
        IReadOnlyList<int> scheduledDays,
        int sessionCount) => new(
        suggestion.Id,
        suggestion.Kind,
        suggestion.WorkoutId,
        workoutName,
        suggestion.ExerciseId,
        suggestion.ExerciseId is { } exerciseId ? exerciseNames.GetValueOrDefault(exerciseId, string.Empty) : string.Empty,
        suggestion.TargetSets,
        suggestion.RepsMin,
        suggestion.RepsMax,
        suggestion.WeightStepKg,
        suggestion.BeforeTargetSets,
        suggestion.BeforeRepsMin,
        suggestion.BeforeRepsMax,
        suggestion.BeforeWeightStepKg,
        ToExerciseChanges(suggestion, exerciseNames),
        scheduledDays,
        sessionCount,
        suggestion.Rationale,
        suggestion.Status);

    public static IEnumerable<Guid> ReferencedExerciseIds(this CoachSuggestion suggestion) =>
        SuggestionSnapshots.Deserialize(suggestion.BeforeExercises)
            .Concat(SuggestionSnapshots.Deserialize(suggestion.AfterExercises))
            .Select(snapshot => snapshot.ExerciseId)
            .Concat(suggestion.ExerciseId is { } exerciseId ? [exerciseId] : []);

    private static IReadOnlyList<CoachSuggestionExerciseDto> ToExerciseChanges(CoachSuggestion suggestion, IReadOnlyDictionary<Guid, string> exerciseNames)
    {
        var before = SuggestionSnapshots.Deserialize(suggestion.BeforeExercises);

        string NameOf(Guid exerciseId) => exerciseNames.GetValueOrDefault(exerciseId, string.Empty);

        if (suggestion.Kind == SuggestionKind.DeleteWorkout)
        {
            return [.. before.Select(entry => new CoachSuggestionExerciseDto(entry.ExerciseId, NameOf(entry.ExerciseId), entry.TargetSets, null, SuggestionExerciseChange.Kept))];
        }

        if (suggestion.Kind != SuggestionKind.ReplaceExercises)
        {
            return [];
        }

        var after = SuggestionSnapshots.Deserialize(suggestion.AfterExercises);
        var previousSets = before.GroupBy(entry => entry.ExerciseId).ToDictionary(group => group.Key, group => group.First().TargetSets);
        var afterIds = after.Select(entry => entry.ExerciseId).ToHashSet();

        var kept = after
            .Where(entry => previousSets.ContainsKey(entry.ExerciseId))
            .Select(entry => new CoachSuggestionExerciseDto(
                entry.ExerciseId,
                NameOf(entry.ExerciseId),
                entry.TargetSets,
                previousSets[entry.ExerciseId] == entry.TargetSets ? null : previousSets[entry.ExerciseId],
                SuggestionExerciseChange.Kept));

        var removed = before
            .Where(entry => !afterIds.Contains(entry.ExerciseId))
            .Select(entry => new CoachSuggestionExerciseDto(entry.ExerciseId, NameOf(entry.ExerciseId), entry.TargetSets, null, SuggestionExerciseChange.Removed));

        var added = after
            .Where(entry => !previousSets.ContainsKey(entry.ExerciseId))
            .Select(entry => new CoachSuggestionExerciseDto(entry.ExerciseId, NameOf(entry.ExerciseId), entry.TargetSets, null, SuggestionExerciseChange.Added));

        return [.. kept, .. removed, .. added];
    }
}
