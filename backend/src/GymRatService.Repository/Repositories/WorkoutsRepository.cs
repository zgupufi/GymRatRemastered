using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Extensions;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class WorkoutsRepository(GymRatDbContext context) : IWorkoutsRepository
{
    public async Task<PagedResult<Workout>> GetByUserAsync(Guid userId, WorkoutFilter filter, CancellationToken cancellationToken)
    {
        var query = context.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId);

        if (!string.IsNullOrWhiteSpace(filter.Search.Term))
        {
            var term = filter.Search.Term.Trim().ToLower();

            query = query.Where(workout => workout.Name.ToLower().Contains(term));
        }

        var direction = filter.Sort.Direction;

        var sorted = (filter.Sort.By ?? WorkoutSortField.CreatedAt) switch
        {
            WorkoutSortField.Name => query.OrderByDirection(workout => workout.Name, direction),
            _ => query.OrderByDirection(workout => workout.Id, direction),
        };

        return await sorted
            .ThenByDirection(workout => workout.Id, direction)
            .Include(workout => workout.Exercises)
            .ThenInclude(entry => entry.Exercise)
            .ToPagedResultAsync(filter.Pagination, cancellationToken);
    }

    public async Task<Workout?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Workouts
            .AsNoTracking()
            .Include(workout => workout.Exercises.OrderBy(entry => entry.Position))
            .ThenInclude(entry => entry.Exercise)
            .FirstOrDefaultAsync(workout => workout.Id == id, cancellationToken);
    }

    public async Task AddAsync(Workout workout, CancellationToken cancellationToken)
    {
        context.Workouts.Add(workout);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        await context.Workouts
            .Where(workout => workout.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(workout => workout.Name, name), cancellationToken);
    }

    public async Task ReplaceExercisesAsync(Guid id, IReadOnlyList<WorkoutExercise> exercises, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.WorkoutExercises
            .Where(entry => entry.WorkoutId == id)
            .ExecuteDeleteAsync(cancellationToken);

        context.WorkoutExercises.AddRange(exercises);

        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await context.Workouts
            .Where(workout => workout.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
