using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface ISessionsRepository
{
    Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(WorkoutSession session, CancellationToken cancellationToken);

    Task ReplaceSetsAsync(Guid sessionId, Guid exerciseId, IReadOnlyList<SessionSet> sets, CancellationToken cancellationToken);

    Task FinishAsync(Guid id, DateTimeOffset finishedAt, int durationSeconds, CancellationToken cancellationToken);

    Task<WorkoutSession?> GetLastFinishedAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSet>> GetExerciseSetsAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken);
}
