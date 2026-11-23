using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class ExportRepository(GymRatDbContext context) : IExportRepository
{
    public async Task<IReadOnlyList<Workout>> GetWorkoutsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId)
            .Include(workout => workout.Exercises)
            .OrderBy(workout => workout.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkoutSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .Include(session => session.Sets)
            .OrderBy(session => session.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetExerciseNamesAsync(IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken)
    {
        return await context.Exercises
            .AsNoTracking()
            .Where(exercise => exerciseIds.Contains(exercise.Id))
            .ToDictionaryAsync(exercise => exercise.Id, exercise => exercise.Name, cancellationToken);
    }
}
