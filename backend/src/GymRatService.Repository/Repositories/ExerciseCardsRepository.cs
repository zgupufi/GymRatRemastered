using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class ExerciseCardsRepository(GymRatDbContext context) : IExerciseCardsRepository
{
    public async Task<IReadOnlyList<ExerciseCard>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await context.Exercises
            .AsNoTracking()
            .OrderBy(exercise => exercise.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExerciseCard?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await context.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(exercise => exercise.Id == id, cancellationToken);
    }
}
