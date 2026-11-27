using GymRatService.Service.Services;
using Microsoft.Extensions.AI;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GymRatService.BLL.Tests.Services;

public class UsageTrackingChatClientTests
{
    private readonly IChatClient _inner = Substitute.For<IChatClient>();

    private readonly ChatMessage[] _messages = [new(ChatRole.User, new string('a', 400))];

    [Fact]
    public async Task GetResponseAsync_AddsUpTheTokensTheProviderReportsForEveryCall()
    {
        // Arrange
        Respond(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")) { Usage = new UsageDetails { TotalTokenCount = 250 } });
        using var client = new UsageTrackingChatClient(_inner);

        // Act
        await client.GetResponseAsync(_messages);
        await client.GetResponseAsync(_messages);

        // Assert
        Assert.Equal(500, client.UsedTokens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public async Task GetResponseAsync_EstimatesInputAndOutput_WhenTheProviderReportsNoUsage(long? reported)
    {
        // Arrange
        Respond(new ChatResponse(new ChatMessage(ChatRole.Assistant, new string('b', 41)))
        {
            Usage = reported is null ? null : new UsageDetails { TotalTokenCount = reported },
        });
        using var client = new UsageTrackingChatClient(_inner);

        // Act
        await client.GetResponseAsync(_messages);

        // Assert
        Assert.Equal(111, client.UsedTokens);
    }

    [Fact]
    public async Task GetResponseAsync_CountsTheInputAndTheMaximumOutput_WhenTheCallerCancelsDuringTheCall()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        using var client = new UsageTrackingChatClient(_inner);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetResponseAsync(_messages, new ChatOptions { MaxOutputTokens = 600 }, cancellation.Token));

        // Assert
        Assert.Equal(700, client.UsedTokens);
    }

    [Fact]
    public async Task GetResponseAsync_CountsNothing_WhenTheProviderFails()
    {
        // Arrange
        _inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("boom"));
        using var client = new UsageTrackingChatClient(_inner);

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync(_messages));

        // Assert
        Assert.Equal(0, client.UsedTokens);
    }

    [Fact]
    public void Dispose_LeavesTheSharedClientOpen()
    {
        // Arrange
        var client = new UsageTrackingChatClient(_inner);

        // Act
        client.Dispose();

        // Assert
        _inner.DidNotReceive().Dispose();
    }

    private void Respond(ChatResponse response) =>
        _inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(response);
}
