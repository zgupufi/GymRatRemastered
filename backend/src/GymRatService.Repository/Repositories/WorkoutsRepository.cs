using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class WorkoutsRepository(GymRatDbContext context) : IWorkoutsRepository
{
    public async Task<IReadOnlyList<Workout>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var workouts = await context.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId)
            .Include(workout => workout.Exercises)
            .ThenInclude(entry => entry.Exercise)
            .ToListAsync(cancellationToken);

        return workouts
            .OrderBy(workout => workout.CreatedAt)
            .ThenBy(workout => workout.Id)
            .ToList();
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
