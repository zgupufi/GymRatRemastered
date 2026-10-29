using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IWorkoutsRepository
{
    Task<PagedResult<Workout>> GetByUserAsync(Guid userId, WorkoutFilter filter, CancellationToken cancellationToken);

    Task<Workout?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Workout workout, CancellationToken cancellationToken);

    Task RenameAsync(Guid id, string name, CancellationToken cancellationToken);

    Task ReplaceExercisesAsync(Guid id, IReadOnlyList<WorkoutExercise> exercises, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
