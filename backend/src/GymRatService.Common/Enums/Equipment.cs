using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Equipment
{
    Barbell,
    Bench,
    Bodyweight,
    Cable,
    Dumbbells,
    Machine,
}
