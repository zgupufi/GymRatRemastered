using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class RefreshTokensRepository(GymRatDbContext context) : IRefreshTokensRepository
{
    private const int RevokePasses = 2;

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        context.RefreshTokens.Add(token);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return await context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<bool> TryRotateAsync(Guid id, RefreshToken successor, DateTimeOffset rotatedAt, int maxRotations, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await context.RefreshTokens
            .Where(token => token.Id == id && token.RevokedAt == null && token.RotationCount < maxRotations)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RotatedAt, token => token.RotatedAt ?? rotatedAt)
                    .SetProperty(token => token.RotationCount, token => token.RotationCount + 1),
                cancellationToken);

        if (claimed == 0)
        {
            return false;
        }

        context.RefreshTokens.Add(successor);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<bool> IsFamilyRevokedAsync(Guid familyId, CancellationToken cancellationToken)
    {
        return await context.RefreshTokens
            .AnyAsync(token => token.FamilyId == familyId && token.RevokedAt != null, cancellationToken);
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        for (var pass = 0; pass < RevokePasses; pass++)
        {
            await context.RefreshTokens
                .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, revokedAt), cancellationToken);
        }
    }

    public async Task DeleteStaleAsync(Guid userId, DateTimeOffset now, TimeSpan revokedRetention, CancellationToken cancellationToken)
    {
        var candidates = await context.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .Select(token => new { token.Id, token.ExpiresAt, token.RevokedAt })
            .ToListAsync(cancellationToken);

        var staleIds = candidates
            .Where(token => token.ExpiresAt <= now || (token.RevokedAt is { } revokedAt && now - revokedAt >= revokedRetention))
            .Select(token => token.Id)
            .ToList();

        if (staleIds.Count > 0)
        {
            await context.RefreshTokens
                .Where(token => staleIds.Contains(token.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
