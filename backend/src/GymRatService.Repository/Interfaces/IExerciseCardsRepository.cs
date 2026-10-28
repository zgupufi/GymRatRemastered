using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IExerciseCardsRepository
{
    Task<IReadOnlyList<ExerciseCard>> GetAllAsync(string? search, string? muscle, CancellationToken cancellationToken);

    Task<ExerciseCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CountExistingAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
