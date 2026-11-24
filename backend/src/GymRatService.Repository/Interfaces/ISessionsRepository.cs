using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface ISessionsRepository
{
    Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkoutSession?> GetActiveAsync(Guid userId, CancellationToken cancellationToken);

    Task<WorkoutSession?> GetOpenAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(WorkoutSession session, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task ReplaceSetsAsync(Guid sessionId, Guid exerciseId, IReadOnlyList<SessionSet> sets, CancellationToken cancellationToken);

    Task FinishAsync(Guid id, DateTimeOffset finishedAt, int durationSeconds, CancellationToken cancellationToken);

    Task<WorkoutSession?> GetLastFinishedAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken);

    Task<int> CountFinishedByWorkoutAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSet>> GetBestSetPerSessionAsync(Guid userId, Guid exerciseId, DateTimeOffset? since, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSet>> GetRecentExerciseSetsAsync(Guid userId, Guid exerciseId, int sessionCount, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSet>> GetRecentSetsAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, int sessionCount, CancellationToken cancellationToken);
}
