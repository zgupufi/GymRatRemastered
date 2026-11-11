using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuggestionStatus
{
    Pending = 0,
    Applied = 1,
    Dismissed = 2,
    Expired = 3,
}
