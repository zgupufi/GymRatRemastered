using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Extensions;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class ExerciseCardsRepository(GymRatDbContext context) : IExerciseCardsRepository
{
    public async Task<PagedResult<ExerciseCard>> GetAllAsync(ExerciseFilter filter, CancellationToken cancellationToken)
    {
        var query = context.Exercises.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search.Term))
        {
            var term = filter.Search.Term.Trim().ToLower();

            query = query.Where(exercise =>
                exercise.Name.ToLower().Contains(term)
                || exercise.MainMuscle.ToLower().Contains(term)
                || (exercise.SubMuscles != null && exercise.SubMuscles.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Muscle))
        {
            var mainMuscle = filter.Muscle.Trim().ToLower();

            query = query.Where(exercise => exercise.MainMuscle.ToLower() == mainMuscle);
        }

        var direction = filter.Sort.Direction;

        var sorted = (filter.Sort.By ?? ExerciseSortField.Name) switch
        {
            ExerciseSortField.MainMuscle => query.OrderByDirection(exercise => exercise.MainMuscle, direction),
            ExerciseSortField.Difficulty => query.OrderByDirection(exercise => exercise.Difficulty, direction),
            _ => query.OrderByDirection(exercise => exercise.Name, direction),
        };

        return await sorted
            .ThenBy(exercise => exercise.Name)
            .ToPagedResultAsync(filter.Pagination, cancellationToken);
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
