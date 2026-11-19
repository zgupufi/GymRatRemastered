using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using Microsoft.Extensions.Options;

namespace GymRatService.BLL.Tests.Services;

public class LoginAttemptLimiterTests
{
    private const string Email = "ana@example.com";

    private const string OtherEmail = "maria@example.com";

    private readonly AdjustableTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly LoginAttemptLimiter _limiter;

    public LoginAttemptLimiterTests()
    {
        var settings = new AuthRateLimitSettings { FailedLoginLimit = 3, FailedLoginWindowSeconds = 900 };
        _limiter = new LoginAttemptLimiter(Options.Create(settings), _timeProvider);
    }

    [Fact]
    public void RegisterAttempt_LetsOnlyTheLimitThrough_WhenMoreAttemptsStartBeforeAnyCompletes()
    {
        // Arrange
        var outcomes = new List<Exception?>();

        // Act
        for (var attempt = 0; attempt < 8; attempt++)
        {
            outcomes.Add(Record.Exception(() => _limiter.RegisterAttempt(Email)));
        }

        // Assert
        Assert.Equal(3, outcomes.Count(outcome => outcome is null));
        Assert.All(outcomes.Skip(3), outcome => Assert.IsType<RateLimitedException>(outcome));
    }

    [Fact]
    public void RegisterAttempt_LetsOnlyTheLimitThrough_WhenAttemptsArriveInParallel()
    {
        // Arrange
        var allowed = 0;

        // Act
        Parallel.For(0, 64, _ =>
        {
            if (Record.Exception(() => _limiter.RegisterAttempt(Email)) is null)
            {
                Interlocked.Increment(ref allowed);
            }
        });

        // Assert
        Assert.Equal(3, allowed);
    }

    [Fact]
    public void RegisterAttempt_ThrowsTooManyAttemptsWithTheTimeUntilTheOldestAttemptExpires_WhenTheLimitIsReached()
    {
        // Arrange
        _limiter.RegisterAttempt(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _limiter.RegisterAttempt(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _limiter.RegisterAttempt(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        var exception = Assert.Throws<RateLimitedException>(() => _limiter.RegisterAttempt(Email));

        // Assert
        Assert.Equal(ErrorCodes.TooManyAttempts, exception.Code);
        Assert.Equal(TimeSpan.FromMinutes(12), exception.RetryAfter);
    }

    [Fact]
    public void RegisterAttempt_DoesNotCountRefusedAttempts()
    {
        // Arrange
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(14));
        Assert.Throws<RateLimitedException>(() => _limiter.RegisterAttempt(Email));
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        var exception = Record.Exception(() => _limiter.RegisterAttempt(Email));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void RegisterAttempt_AllowsAnotherEmail_WhenOneEmailReachedTheLimit()
    {
        // Arrange
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);

        // Act
        var exception = Record.Exception(() => _limiter.RegisterAttempt(OtherEmail));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void RegisterAttempt_AllowsAgain_WhenTheOldestAttemptLeavesTheWindow()
    {
        // Arrange
        _limiter.RegisterAttempt(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        var exception = Record.Exception(() => _limiter.RegisterAttempt(Email));

        // Assert
        Assert.Null(exception);
        Assert.Throws<RateLimitedException>(() => _limiter.RegisterAttempt(Email));
    }

    [Fact]
    public void RegisterAttempt_ForgetsEmailsWhoseAttemptsExpired()
    {
        // Arrange
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(OtherEmail);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        _limiter.RegisterAttempt("ion@example.com");
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        _limiter.RegisterAttempt("elena@example.com");

        // Assert
        Assert.Equal(2, _limiter.TrackedEmails);
    }

    [Fact]
    public void RegisterAttempt_TracksAFixedSizeKey_WhenTheEmailIsVeryLong()
    {
        // Arrange
        var longEmail = new string('a', 1_000_000) + "@example.com";
        _limiter.RegisterAttempt(longEmail);
        _limiter.RegisterAttempt(longEmail);
        _limiter.RegisterAttempt(longEmail);

        // Act
        var exception = Record.Exception(() => _limiter.RegisterAttempt(longEmail));

        // Assert
        Assert.IsType<RateLimitedException>(exception);
        Assert.Equal(1, _limiter.TrackedEmails);
    }

    [Fact]
    public void Reset_ClearsOnlyTheGivenEmail()
    {
        // Arrange
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(Email);
        _limiter.RegisterAttempt(OtherEmail);
        _limiter.RegisterAttempt(OtherEmail);
        _limiter.RegisterAttempt(OtherEmail);

        // Act
        _limiter.Reset(Email);

        // Assert
        Assert.Null(Record.Exception(() => _limiter.RegisterAttempt(Email)));
        Assert.Throws<RateLimitedException>(() => _limiter.RegisterAttempt(OtherEmail));
    }
}
