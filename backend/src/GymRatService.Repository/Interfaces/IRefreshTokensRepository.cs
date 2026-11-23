using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IRefreshTokensRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<bool> TryRotateAsync(Guid id, RefreshToken successor, DateTimeOffset rotatedAt, int maxRotations, CancellationToken cancellationToken);

    Task<bool> IsFamilyRevokedAsync(Guid familyId, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    Task DeleteStaleAsync(Guid userId, DateTimeOffset now, TimeSpan revokedRetention, CancellationToken cancellationToken);
}
