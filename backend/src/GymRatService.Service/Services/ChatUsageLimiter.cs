using System.Globalization;
using GymRatService.Common.Errors;
using GymRatService.Common.Settings;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Usage;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Services;

public class ChatUsageLimiter(ICoachUsageRepository usageRepository, IOptions<AiCoachSettings> options, TimeProvider timeProvider) : IChatUsageLimiter
{
    private const int MaximumAttempts = 10;

    private const char Separator = ',';

    public async Task<ChatUsageReservation> ReserveAsync(Guid userId, long estimatedTokens, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var stored = await usageRepository.GetByUserAsync(userId, cancellationToken);
            var usage = Apply(stored, userId, today, now, estimatedTokens);

            var saved = stored is null
                ? await usageRepository.TryAddAsync(usage, cancellationToken)
                : await usageRepository.TryUpdateAsync(usage, stored.Version, cancellationToken);

            if (saved)
            {
                return new ChatUsageReservation(userId, today, estimatedTokens);
            }
        }

        throw new BusinessLogicException(ErrorCodes.ChatRateLimited);
    }

    public async Task SettleAsync(ChatUsageReservation reservation, long usedTokens, CancellationToken cancellationToken)
    {
        var difference = usedTokens - reservation.Tokens;

        if (difference != 0)
        {
            await usageRepository.AdjustTokensAsync(reservation.UserId, reservation.Day, difference, cancellationToken);
        }
    }

    private CoachUsage Apply(CoachUsage? stored, Guid userId, DateOnly today, DateTimeOffset now, long estimatedTokens)
    {
        var settings = options.Value;
        var current = stored?.Day == today ? stored : null;
        var requestCount = current?.RequestCount ?? 0;
        var tokensUsed = current?.TokensUsed ?? 0;

        if (tokensUsed + estimatedTokens > settings.DailyTokenBudget)
        {
            throw new BusinessLogicException(ErrorCodes.ChatTokenBudgetExceeded);
        }

        var windowStart = (now - TimeSpan.FromSeconds(settings.RateLimitWindowSeconds)).ToUnixTimeMilliseconds();
        var recentRequests = (stored?.RecentRequests ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => long.Parse(value, CultureInfo.InvariantCulture))
            .Where(requestedAt => requestedAt > windowStart)
            .ToList();

        if (recentRequests.Count >= settings.RateLimitRequests || requestCount >= settings.DailyRequestLimit)
        {
            throw new BusinessLogicException(ErrorCodes.ChatRateLimited);
        }

        recentRequests.Add(now.ToUnixTimeMilliseconds());

        return new CoachUsage
        {
            UserId = userId,
            Day = today,
            RequestCount = requestCount + 1,
            TokensUsed = tokensUsed + estimatedTokens,
            RecentRequests = string.Join(Separator, recentRequests.Select(requestedAt => requestedAt.ToString(CultureInfo.InvariantCulture))),
            Version = stored?.Version ?? 0,
        };
    }
}
