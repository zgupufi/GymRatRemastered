using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface ICoachUsageRepository
{
    Task<CoachUsage?> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(CoachUsage usage, CancellationToken cancellationToken);

    Task<bool> TryUpdateAsync(CoachUsage usage, int expectedVersion, CancellationToken cancellationToken);

    Task AdjustTokensAsync(Guid userId, DateOnly day, long difference, CancellationToken cancellationToken);
}
