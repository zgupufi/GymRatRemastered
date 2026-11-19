using System.Security.Cryptography;
using System.Text;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Services;

public class LoginAttemptLimiter(IOptions<AuthRateLimitSettings> options, TimeProvider timeProvider) : ILoginAttemptLimiter
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _attempts = [];

    private readonly Lock _lock = new();

    private DateTimeOffset _nextSweep;

    public int TrackedEmails
    {
        get
        {
            lock (_lock)
            {
                return _attempts.Count;
            }
        }
    }

    public void RegisterAttempt(string email)
    {
        var settings = options.Value;
        var window = TimeSpan.FromSeconds(settings.FailedLoginWindowSeconds);
        var now = timeProvider.GetUtcNow();
        var windowStart = now - window;
        var key = GetKey(email);

        lock (_lock)
        {
            if (now >= _nextSweep)
            {
                RemoveExpiredEmails(windowStart);
                _nextSweep = now + window;
            }

            if (!_attempts.TryGetValue(key, out var attempts))
            {
                attempts = new Queue<DateTimeOffset>();
                _attempts[key] = attempts;
            }

            RemoveExpiredAttempts(attempts, windowStart);

            if (attempts.Count >= settings.FailedLoginLimit)
            {
                throw new RateLimitedException(ErrorCodes.TooManyAttempts, attempts.Peek() + window - now);
            }

            attempts.Enqueue(now);
        }
    }

    public void Reset(string email)
    {
        var key = GetKey(email);

        lock (_lock)
        {
            _attempts.Remove(key);
        }
    }

    private static string GetKey(string email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email)));

    private static void RemoveExpiredAttempts(Queue<DateTimeOffset> attempts, DateTimeOffset windowStart)
    {
        while (attempts.Count > 0 && attempts.Peek() <= windowStart)
        {
            attempts.Dequeue();
        }
    }

    private void RemoveExpiredEmails(DateTimeOffset windowStart)
    {
        foreach (var (key, attempts) in _attempts)
        {
            RemoveExpiredAttempts(attempts, windowStart);

            if (attempts.Count == 0)
            {
                _attempts.Remove(key);
            }
        }
    }
}
