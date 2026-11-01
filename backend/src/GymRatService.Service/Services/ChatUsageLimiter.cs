using System.Collections.Concurrent;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Services;

public class ChatUsageLimiter(IOptions<AiCoachSettings> options, TimeProvider timeProvider) : IChatUsageLimiter
{
    private readonly ConcurrentDictionary<Guid, UserUsage> _usage = new();

    public void EnsureAllowed(Guid userId)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var usage = _usage.GetOrAdd(userId, _ => new UserUsage());

        lock (usage)
        {
            if (usage.Day != today)
            {
                usage.Day = today;
                usage.Tokens = 0;
            }

            if (usage.Tokens >= settings.DailyTokenBudget)
            {
                throw new BusinessLogicException(ErrorCodes.ChatTokenBudgetExceeded);
            }

            var windowStart = now - TimeSpan.FromSeconds(settings.RateLimitWindowSeconds);

            while (usage.Requests.Count > 0 && usage.Requests.Peek() <= windowStart)
            {
                usage.Requests.Dequeue();
            }

            if (usage.Requests.Count >= settings.RateLimitRequests)
            {
                throw new BusinessLogicException(ErrorCodes.ChatRateLimited);
            }

            usage.Requests.Enqueue(now);
        }
    }

    public void RecordTokens(Guid userId, long tokens)
    {
        var usage = _usage.GetOrAdd(userId, _ => new UserUsage());

        lock (usage)
        {
            usage.Tokens += tokens;
        }
    }

    private sealed class UserUsage
    {
        public Queue<DateTimeOffset> Requests { get; } = new();

        public DateOnly Day { get; set; }

        public long Tokens { get; set; }
    }
}
