using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface ISplitRepository
{
    Task<IReadOnlyList<SplitDay>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task SetDayAsync(Guid userId, int day, Guid workoutId, CancellationToken cancellationToken);

    Task ClearDayAsync(Guid userId, int day, CancellationToken cancellationToken);
}
