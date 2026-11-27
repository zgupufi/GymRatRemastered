using GymRatService.Repository.Entities;
using GymRatService.Repository.Extensions;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class CoachUsageRepository(GymRatDbContext context) : ICoachUsageRepository
{
    public async Task<CoachUsage?> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.CoachUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(usage => usage.UserId == userId, cancellationToken);
    }

    public async Task<bool> TryAddAsync(CoachUsage usage, CancellationToken cancellationToken)
    {
        context.CoachUsages.Add(usage);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return false;
        }
        finally
        {
            context.Entry(usage).State = EntityState.Detached;
        }
    }

    public async Task<bool> TryUpdateAsync(CoachUsage usage, int expectedVersion, CancellationToken cancellationToken)
    {
        var updated = await context.CoachUsages
            .Where(stored => stored.UserId == usage.UserId && stored.Version == expectedVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(stored => stored.Day, usage.Day)
                    .SetProperty(stored => stored.RequestCount, usage.RequestCount)
                    .SetProperty(stored => stored.TokensUsed, usage.TokensUsed)
                    .SetProperty(stored => stored.RecentRequests, usage.RecentRequests)
                    .SetProperty(stored => stored.Version, expectedVersion + 1),
                cancellationToken);

        return updated > 0;
    }

    public async Task AdjustTokensAsync(Guid userId, DateOnly day, long difference, CancellationToken cancellationToken)
    {
        await context.CoachUsages
            .Where(usage => usage.UserId == userId && usage.Day == day)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(usage => usage.TokensUsed, usage => usage.TokensUsed + difference)
                    .SetProperty(usage => usage.Version, usage => usage.Version + 1),
                cancellationToken);
    }
}
