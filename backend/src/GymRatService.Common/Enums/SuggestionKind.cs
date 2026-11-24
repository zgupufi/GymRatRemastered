using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuggestionKind
{
    ExerciseChange = 0,
    ReplaceExercises = 1,
    DeleteWorkout = 2,
}
