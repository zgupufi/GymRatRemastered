using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Chat;

public sealed record ChatHistoryItem(ChatRole Role, string Content);
