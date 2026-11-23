using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovementPattern
{
    Push,
    Pull,
    Squat,
    Hinge,
    Core,
    Isolation,
}
