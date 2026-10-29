using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class SplitRepository(GymRatDbContext context) : ISplitRepository
{
    public async Task<IReadOnlyList<SplitDay>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.SplitDays
            .AsNoTracking()
            .Where(splitDay => splitDay.UserId == userId)
            .Include(splitDay => splitDay.Workout)
            .ThenInclude(workout => workout!.Exercises)
            .ThenInclude(entry => entry.Exercise)
            .OrderBy(splitDay => splitDay.Day)
            .ToListAsync(cancellationToken);
    }

    public async Task SetDayAsync(Guid userId, int day, Guid workoutId, CancellationToken cancellationToken)
    {
        var updated = await context.SplitDays
            .Where(splitDay => splitDay.UserId == userId && splitDay.Day == day)
            .ExecuteUpdateAsync(setters => setters.SetProperty(splitDay => splitDay.WorkoutId, workoutId), cancellationToken);

        if (updated > 0)
        {
            return;
        }

        context.SplitDays.Add(new SplitDay { UserId = userId, Day = day, WorkoutId = workoutId });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearDayAsync(Guid userId, int day, CancellationToken cancellationToken)
    {
        await context.SplitDays
            .Where(splitDay => splitDay.UserId == userId && splitDay.Day == day)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
