using GymRatService.Common.DTOs.Coach;

namespace GymRatService.Common.DTOs.Chat;

public sealed record ChatMessageResponse(string Reply, bool WorkoutsChanged, IReadOnlyList<CoachSuggestionDto> Suggestions);
