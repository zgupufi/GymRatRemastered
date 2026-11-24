using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuggestionExerciseChange
{
    Kept = 0,
    Removed = 1,
    Added = 2,
}
