using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class ExerciseCardsRepository(GymRatDbContext context) : IExerciseCardsRepository
{
    public async Task<IReadOnlyList<ExerciseCard>> GetAllAsync(string? search, string? muscle, CancellationToken cancellationToken)
    {
        var query = context.Exercises.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();

            query = query.Where(exercise =>
                exercise.Name.ToLower().Contains(term)
                || exercise.MainMuscle.ToLower().Contains(term)
                || (exercise.SubMuscles != null && exercise.SubMuscles.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(muscle))
        {
            var mainMuscle = muscle.Trim().ToLower();

            query = query.Where(exercise => exercise.MainMuscle.ToLower() == mainMuscle);
        }

        return await query
            .OrderBy(exercise => exercise.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExerciseCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(exercise => exercise.Id == id, cancellationToken);
    }

    public async Task<int> CountExistingAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        return await context.Exercises.CountAsync(exercise => ids.Contains(exercise.Id), cancellationToken);
    }
}
