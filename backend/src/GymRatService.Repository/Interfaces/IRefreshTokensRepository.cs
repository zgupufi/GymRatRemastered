using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IRefreshTokensRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task MarkRotatedAsync(Guid id, DateTimeOffset rotatedAt, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
