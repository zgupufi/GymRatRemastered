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
        var query = context.Exercises
            .AsNoTracking()
            .Include(exercise => exercise.SecondaryMuscles)
            .Include(exercise => exercise.Translations)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(filter.Search.Term))
        {
            var term = filter.Search.Term.Trim().ToLower();
            var muscleTerm = term.Replace(" ", string.Empty);

            var matchingGroups = Enum.GetValues<MuscleGroup>()
                .Where(group => group.ToString().Contains(muscleTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var matchingMuscles = Enum.GetValues<Muscle>()
                .Where(muscle => muscle.ToString().Contains(muscleTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            query = query.Where(exercise =>
                exercise.Name.ToLower().Contains(term)
                || matchingGroups.Contains(exercise.MainMuscle)
                || exercise.SecondaryMuscles.Any(secondary => matchingMuscles.Contains(secondary.Muscle)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Muscle))
        {
            query = Enum.TryParse<MuscleGroup>(filter.Muscle.Trim(), true, out var mainMuscle)
                ? query.Where(exercise => exercise.MainMuscle == mainMuscle)
                : query.Where(_ => false);
        }

        if (filter.Equipment is { } equipment)
        {
            query = query.Where(exercise => exercise.Equipment == equipment);
        }

        if (filter.Pattern is { } pattern)
        {
            query = query.Where(exercise => exercise.MovementPattern == pattern);
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
            .Include(exercise => exercise.SecondaryMuscles)
            .Include(exercise => exercise.Translations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(exercise => exercise.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExerciseCard>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        return await context.Exercises
            .AsNoTracking()
            .Where(exercise => ids.Contains(exercise.Id))
            .ToListAsync(cancellationToken);
    }
}
