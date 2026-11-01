namespace GymRatService.Common.DTOs.Chat;

public sealed record ChatMessageRequest(string Message, IReadOnlyList<ChatHistoryItem>? History);
