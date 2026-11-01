using GymRatService.Common.Errors;
using GymRatService.Service.Exceptions;
using Microsoft.Extensions.AI;

namespace GymRatService.API.Extensions;

public sealed class UnconfiguredChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new BusinessLogicException(ErrorCodes.ChatProviderUnavailable);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new BusinessLogicException(ErrorCodes.ChatProviderUnavailable);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
