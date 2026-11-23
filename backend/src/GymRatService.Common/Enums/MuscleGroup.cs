using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MuscleGroup
{
    Back,
    Biceps,
    Chest,
    Core,
    Legs,
    Shoulders,
    Triceps,
}
