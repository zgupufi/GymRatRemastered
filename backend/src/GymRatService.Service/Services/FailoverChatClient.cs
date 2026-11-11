using System.ClientModel;
using GymRatService.Common.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Services;

public sealed class FailoverChatClient(IChatClient inner, IOptions<AiCoachSettings> options, ILogger logger) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var history = messages.ToList();
        var hasFallback = !string.IsNullOrWhiteSpace(settings.FallbackModel);

        try
        {
            var response = await base.GetResponseAsync(history, WithModel(chatOptions, settings.Model), cancellationToken);

            if (hasFallback && IsEmpty(response))
            {
                logger.LogWarning("AI coach model {Model} returned an empty reply, trying {FallbackModel}", settings.Model, settings.FallbackModel);
            }

            if (!hasFallback || !IsEmpty(response))
            {
                return response;
            }
        }
        catch (Exception exception) when (hasFallback && ShouldFailOver(exception, cancellationToken))
        {
            logger.LogWarning(exception, "AI coach model {Model} failed, trying {FallbackModel}", settings.Model, settings.FallbackModel);
        }

        return await base.GetResponseAsync(history, WithModel(chatOptions, settings.FallbackModel), cancellationToken);
    }

    private static ChatOptions WithModel(ChatOptions? chatOptions, string model)
    {
        var clone = chatOptions?.Clone() ?? new ChatOptions();
        clone.ModelId = model;

        return clone;
    }

    private static bool IsEmpty(ChatResponse response) =>
        string.IsNullOrWhiteSpace(response.Text)
        && !response.Messages.Any(message => message.Contents.OfType<FunctionCallContent>().Any());

    private static bool ShouldFailOver(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        TimeoutException => true,
        ArgumentOutOfRangeException => true,
        HttpRequestException http => http.StatusCode is null || IsRetryableStatus((int)http.StatusCode),
        ClientResultException client => IsRetryableStatus(client.Status),
        _ => false,
    };

    private static bool IsRetryableStatus(int status) => status == 0 || status == 429 || status >= 500;
}
