using GymRatService.Common.DTOs.Chat;

namespace GymRatService.Service.Interfaces;

public interface IChatService
{
    Task<ChatMessageResponse> SendMessageAsync(Guid userId, ChatMessageRequest request, CancellationToken cancellationToken);
}
