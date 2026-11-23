using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IExportRepository
{
    Task<IReadOnlyList<Workout>> GetWorkoutsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkoutSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetExerciseNamesAsync(IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken);
}
