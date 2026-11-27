using GymRatService.Service.Usage;
using Microsoft.Extensions.AI;

namespace GymRatService.Service.Services;

public sealed class UsageTrackingChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public long UsedTokens { get; private set; }

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var sent = messages.ToList();

        try
        {
            var response = await base.GetResponseAsync(sent, options, cancellationToken);

            UsedTokens += response.Usage?.TotalTokenCount is > 0 and var reported
                ? reported
                : ChatTokenEstimator.EstimateInput(sent, options) + ChatTokenEstimator.EstimateOutput(response);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            UsedTokens += ChatTokenEstimator.EstimateInput(sent, options);

            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
    }
}
