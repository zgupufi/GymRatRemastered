using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IExerciseCardsRepository
{
    Task<PagedResult<ExerciseCard>> GetAllAsync(ExerciseFilter filter, CancellationToken cancellationToken);

    Task<ExerciseCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CountExistingAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
