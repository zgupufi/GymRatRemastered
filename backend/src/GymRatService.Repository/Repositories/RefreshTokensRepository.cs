using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class RefreshTokensRepository(GymRatDbContext context) : IRefreshTokensRepository
{
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

    public async Task MarkRotatedAsync(Guid id, DateTimeOffset rotatedAt, CancellationToken cancellationToken)
    {
        await context.RefreshTokens
            .Where(token => token.Id == id && token.RotatedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RotatedAt, rotatedAt), cancellationToken);
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        await context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, revokedAt), cancellationToken);
    }

    public async Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await context.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .Select(token => new { token.Id, token.ExpiresAt })
            .ToListAsync(cancellationToken);

        var expiredIds = candidates.Where(token => token.ExpiresAt <= now).Select(token => token.Id).ToList();

        if (expiredIds.Count > 0)
        {
            await context.RefreshTokens
                .Where(token => expiredIds.Contains(token.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
