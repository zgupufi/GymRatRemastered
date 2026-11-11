using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProgressionReason
{
    NoHistory = 0,
    Hold = 1,
    IncreaseWeight = 2,
    AddRep = 3,
    Stalled = 4,
}
