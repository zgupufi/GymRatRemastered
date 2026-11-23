using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Difficulty
{
    Beginner,
    Intermediate,
    Advanced,
}
