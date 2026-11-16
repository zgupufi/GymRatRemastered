using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Services;

public class LoginAttemptLimiter(IOptions<AuthRateLimitSettings> options, TimeProvider timeProvider) : ILoginAttemptLimiter
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _failures = [];

    private readonly Lock _lock = new();

    public void EnsureAllowed(string email)
    {
        var settings = options.Value;
        var window = TimeSpan.FromSeconds(settings.FailedLoginWindowSeconds);
        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            RemoveExpired(now - window);

            if (_failures.TryGetValue(email, out var failures) && failures.Count >= settings.FailedLoginLimit)
            {
                throw new RateLimitedException(ErrorCodes.TooManyAttempts, failures.Peek() + window - now);
            }
        }
    }

    public void RecordFailure(string email)
    {
        var window = TimeSpan.FromSeconds(options.Value.FailedLoginWindowSeconds);
        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            RemoveExpired(now - window);

            if (!_failures.TryGetValue(email, out var failures))
            {
                failures = new Queue<DateTimeOffset>();
                _failures[email] = failures;
            }

            failures.Enqueue(now);
        }
    }

    public void Reset(string email)
    {
        lock (_lock)
        {
            _failures.Remove(email);
        }
    }

    private void RemoveExpired(DateTimeOffset windowStart)
    {
        foreach (var (email, failures) in _failures)
        {
            while (failures.Count > 0 && failures.Peek() <= windowStart)
            {
                failures.Dequeue();
            }

            if (failures.Count == 0)
            {
                _failures.Remove(email);
            }
        }
    }
}
