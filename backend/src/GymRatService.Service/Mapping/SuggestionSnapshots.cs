using System.Text.Json;
using GymRatService.Common.DTOs.Coach;

namespace GymRatService.Service.Mapping;

internal static class SuggestionSnapshots
{
    public static string Serialize(IReadOnlyList<SuggestionExerciseSnapshot> snapshots) =>
        JsonSerializer.Serialize(snapshots);

    public static IReadOnlyList<SuggestionExerciseSnapshot> Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<SuggestionExerciseSnapshot>>(json) ?? [];
}
