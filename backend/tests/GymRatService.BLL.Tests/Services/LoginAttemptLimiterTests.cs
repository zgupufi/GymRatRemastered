using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using Microsoft.Extensions.Options;

namespace GymRatService.BLL.Tests.Services;

public class LoginAttemptLimiterTests
{
    private const string Email = "ana@example.com";

    private readonly AdjustableTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly LoginAttemptLimiter _limiter;

    public LoginAttemptLimiterTests()
    {
        var settings = new AuthRateLimitSettings { FailedLoginLimit = 3, FailedLoginWindowSeconds = 900 };
        _limiter = new LoginAttemptLimiter(Options.Create(settings), _timeProvider);
    }

    [Fact]
    public void EnsureAllowed_DoesNotThrow_WhenThereAreNoFailures()
    {
        // Act
        var exception = Record.Exception(() => _limiter.EnsureAllowed(Email));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAllowed_DoesNotThrow_WhenTheFailuresAreBelowTheLimit()
    {
        // Arrange
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);

        // Act
        var exception = Record.Exception(() => _limiter.EnsureAllowed(Email));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAllowed_ThrowsTooManyAttemptsWithTheTimeUntilTheOldestFailureExpires_WhenTheLimitIsReached()
    {
        // Arrange
        _limiter.RecordFailure(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _limiter.RecordFailure(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _limiter.RecordFailure(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        var exception = Assert.Throws<RateLimitedException>(() => _limiter.EnsureAllowed(Email));

        // Assert
        Assert.Equal(ErrorCodes.TooManyAttempts, exception.Code);
        Assert.Equal(TimeSpan.FromMinutes(12), exception.RetryAfter);
    }

    [Fact]
    public void EnsureAllowed_DoesNotThrowForAnotherEmail_WhenOneEmailReachedTheLimit()
    {
        // Arrange
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);

        // Act
        var exception = Record.Exception(() => _limiter.EnsureAllowed("maria@example.com"));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAllowed_AllowsAgain_WhenTheOldestFailureLeavesTheWindow()
    {
        // Arrange
        _limiter.RecordFailure(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        var exception = Record.Exception(() => _limiter.EnsureAllowed(Email));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void RecordFailure_StartsANewCount_WhenEveryEarlierFailureExpired()
    {
        // Arrange
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);

        // Act
        var exception = Record.Exception(() => _limiter.EnsureAllowed(Email));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Reset_ClearsOnlyTheGivenEmail()
    {
        // Arrange
        const string otherEmail = "maria@example.com";
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(Email);
        _limiter.RecordFailure(otherEmail);
        _limiter.RecordFailure(otherEmail);
        _limiter.RecordFailure(otherEmail);

        // Act
        _limiter.Reset(Email);

        // Assert
        Assert.Null(Record.Exception(() => _limiter.EnsureAllowed(Email)));
        Assert.Throws<RateLimitedException>(() => _limiter.EnsureAllowed(otherEmail));
    }
}
