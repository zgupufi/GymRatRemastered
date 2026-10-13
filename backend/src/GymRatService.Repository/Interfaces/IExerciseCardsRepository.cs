using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface IExerciseCardsRepository
{
    Task<IReadOnlyList<ExerciseCard>> GetAllAsync(CancellationToken cancellationToken);

    Task<ExerciseCard?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
