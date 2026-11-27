using GymRatService.Service.Usage;

namespace GymRatService.Service.Interfaces;

public interface IChatUsageLimiter
{
    Task<ChatUsageReservation> ReserveAsync(Guid userId, long estimatedTokens, CancellationToken cancellationToken);

    Task SettleAsync(ChatUsageReservation reservation, long usedTokens, CancellationToken cancellationToken);
}
