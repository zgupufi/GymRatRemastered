using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using GymRatService.Service.Usage;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class ChatUsageLimiterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly ICoachUsageRepository _repository = Substitute.For<ICoachUsageRepository>();
    private readonly AiCoachSettings _settings = new() { DailyTokenBudget = 1000, RateLimitRequests = 2, RateLimitWindowSeconds = 60, DailyRequestLimit = 10 };
    private readonly ChatUsageLimiter _limiter;

    public ChatUsageLimiterTests()
    {
        _limiter = new ChatUsageLimiter(_repository, Options.Create(_settings), new AdjustableTimeProvider(Now));
    }

    [Fact]
    public async Task ReserveAsync_StoresTheFirstRequestOfAUser()
    {
        // Arrange
        CoachUsage? added = null;
        _repository.TryAddAsync(Arg.Do<CoachUsage>(usage => added = usage), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var reservation = await _limiter.ReserveAsync(_userId, 400, CancellationToken.None);

        // Assert
        Assert.Equal(new ChatUsageReservation(_userId, new DateOnly(2026, 11, 27), 400), reservation);
        Assert.NotNull(added);
        Assert.Equal((_userId, new DateOnly(2026, 11, 27), 1, 400L), (added.UserId, added.Day, added.RequestCount, added.TokensUsed));
        Assert.Equal(Now.ToUnixTimeMilliseconds().ToString(), added.RecentRequests);
    }

    [Fact]
    public async Task ReserveAsync_AcceptsARequestThatFillsTheBudgetExactlyAndRefusesOneTokenMore()
    {
        // Arrange
        _repository.GetByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(Stored(new DateOnly(2026, 11, 27), tokens: 600));
        _repository.TryUpdateAsync(Arg.Any<CoachUsage>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _limiter.ReserveAsync(_userId, 400, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _limiter.ReserveAsync(_userId, 401, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ChatTokenBudgetExceeded, exception.Code);
        await _repository.Received(1).TryUpdateAsync(Arg.Is<CoachUsage>(usage => usage.TokensUsed == 1000), 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReserveAsync_ResetsTheDailyCountersOnANewDay()
    {
        // Arrange
        _repository.GetByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(Stored(new DateOnly(2026, 11, 26), tokens: 1000, requests: 10));
        _repository.TryUpdateAsync(Arg.Any<CoachUsage>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _limiter.ReserveAsync(_userId, 400, CancellationToken.None);

        // Assert
        await _repository.Received(1).TryUpdateAsync(
            Arg.Is<CoachUsage>(usage => usage.Day == new DateOnly(2026, 11, 27) && usage.TokensUsed == 400 && usage.RequestCount == 1),
            7,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReserveAsync_CountsOnlyTheRequestsInsideTheWindow()
    {
        // Arrange
        var expired = Now.AddSeconds(-60).ToUnixTimeMilliseconds();
        var recent = Now.AddSeconds(-59).ToUnixTimeMilliseconds();
        CoachUsage? updated = null;
        _repository.GetByUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Stored(new DateOnly(2026, 11, 27), recentRequests: $"{expired},{recent}"));
        _repository.TryUpdateAsync(Arg.Do<CoachUsage>(usage => updated = usage), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _limiter.ReserveAsync(_userId, 1, CancellationToken.None);

        // Assert
        Assert.Equal($"{recent},{Now.ToUnixTimeMilliseconds()}", updated?.RecentRequests);
    }

    [Fact]
    public async Task ReserveAsync_KeepsTheWindowAcrossMidnight()
    {
        // Arrange
        var recent = Now.AddSeconds(-1).ToUnixTimeMilliseconds();
        _repository.GetByUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Stored(new DateOnly(2026, 11, 26), recentRequests: $"{recent},{recent}"));

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _limiter.ReserveAsync(_userId, 1, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ChatRateLimited, exception.Code);
    }

    [Fact]
    public async Task ReserveAsync_ReadsTheUsageAgain_WhenAnotherRequestSavedFirst()
    {
        // Arrange
        _repository.GetByUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(null, Stored(new DateOnly(2026, 11, 27), tokens: 300, requests: 1));
        _repository.TryAddAsync(Arg.Any<CoachUsage>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryUpdateAsync(Arg.Any<CoachUsage>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _limiter.ReserveAsync(_userId, 200, CancellationToken.None);

        // Assert
        await _repository.Received(1).TryUpdateAsync(
            Arg.Is<CoachUsage>(usage => usage.TokensUsed == 500 && usage.RequestCount == 2),
            7,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReserveAsync_RefusesTheRequest_WhenItKeepsLosingToOtherRequests()
    {
        // Arrange
        _repository.TryAddAsync(Arg.Any<CoachUsage>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => _limiter.ReserveAsync(_userId, 1, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.ChatRateLimited, exception.Code);
        await _repository.Received(10).TryAddAsync(Arg.Any<CoachUsage>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(100, -300)]
    [InlineData(900, 500)]
    public async Task SettleAsync_AdjustsTheStoredTokensByTheDifference(long used, long difference)
    {
        // Arrange
        var reservation = new ChatUsageReservation(_userId, new DateOnly(2026, 11, 27), 400);

        // Act
        await _limiter.SettleAsync(reservation, used, CancellationToken.None);

        // Assert
        await _repository.Received(1).AdjustTokensAsync(_userId, reservation.Day, difference, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettleAsync_WritesNothing_WhenTheEstimateWasExact()
    {
        // Arrange
        var reservation = new ChatUsageReservation(_userId, new DateOnly(2026, 11, 27), 400);

        // Act
        await _limiter.SettleAsync(reservation, 400, CancellationToken.None);

        // Assert
        await _repository.DidNotReceiveWithAnyArgs().AdjustTokensAsync(default, default, default, default);
    }

    private CoachUsage Stored(DateOnly day, long tokens = 0, int requests = 0, string recentRequests = "") => new()
    {
        UserId = _userId,
        Day = day,
        TokensUsed = tokens,
        RequestCount = requests,
        RecentRequests = recentRequests,
        Version = 7,
    };
}
