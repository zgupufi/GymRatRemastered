using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Services;
using GymRatService.Service.Usage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GymRatService.BLL.Tests.Services;

public class ChatServiceTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IChatUsageLimiter _usageLimiter = Substitute.For<IChatUsageLimiter>();
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        var reservation = new ChatUsageReservation(Guid.CreateVersion7(), new DateOnly(2026, 11, 27), 1000);
        _usageLimiter.ReserveAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(reservation);
        _usageLimiter.SettleAsync(Arg.Any<ChatUsageReservation>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database is down"));

        _service = new ChatService(
            _chatClient,
            Substitute.For<IWorkoutsService>(),
            Substitute.For<ISplitService>(),
            Substitute.For<IExercisesService>(),
            Substitute.For<ISessionsService>(),
            Substitute.For<ICoachService>(),
            Substitute.For<IInputValidator>(),
            _usageLimiter,
            Options.Create(new AiCoachSettings { FallbackModel = string.Empty }),
            NullLogger<ChatService>.Instance);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsTheReply_WhenTheUsageCannotBeSettled()
    {
        // Arrange
        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Train hard.")));

        // Act
        var response = await _service.SendMessageAsync(Guid.CreateVersion7(), new ChatMessageRequest("Hi", null), CancellationToken.None);

        // Assert
        Assert.Equal("Train hard.", response.Reply);
        await _usageLimiter.ReceivedWithAnyArgs(1).SettleAsync(default!, default, default);
    }

    [Fact]
    public async Task SendMessageAsync_KeepsTheProviderError_WhenTheUsageCannotBeSettled()
    {
        // Arrange
        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("boom"));

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() =>
            _service.SendMessageAsync(Guid.CreateVersion7(), new ChatMessageRequest("Hi", null), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ChatProviderUnavailable, exception.Code);
        await _usageLimiter.ReceivedWithAnyArgs(1).SettleAsync(default!, default, default);
    }
}
