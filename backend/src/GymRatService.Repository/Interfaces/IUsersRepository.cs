using GymRatService.Common.Enums;
using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IUsersRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken);

    Task UpdateSettingsAsync(Guid id, WeightUnit weightUnit, int defaultRestTimeSeconds, CancellationToken cancellationToken);
}
