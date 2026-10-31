using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class SessionsRepository(GymRatDbContext context) : ISessionsRepository
{
    public async Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.WorkoutSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    public async Task AddAsync(WorkoutSession session, CancellationToken cancellationToken)
    {
        context.WorkoutSessions.Add(session);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceSetsAsync(Guid sessionId, Guid exerciseId, IReadOnlyList<SessionSet> sets, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.SessionSets
            .Where(set => set.SessionId == sessionId && set.ExerciseId == exerciseId)
            .ExecuteDeleteAsync(cancellationToken);

        context.SessionSets.AddRange(sets);

        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FinishAsync(Guid id, DateTimeOffset finishedAt, int durationSeconds, CancellationToken cancellationToken)
    {
        await context.WorkoutSessions
            .Where(session => session.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.FinishedAt, finishedAt)
                    .SetProperty(session => session.DurationSeconds, durationSeconds),
                cancellationToken);
    }

    public async Task<WorkoutSession?> GetLastFinishedAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken)
    {
        return await context.WorkoutSessions
            .AsNoTracking()
            .Include(session => session.Sets)
            .Where(session => session.UserId == userId && session.WorkoutId == workoutId && session.DurationSeconds != null)
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionSet>> GetExerciseSetsAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken)
    {
        return await context.SessionSets
            .AsNoTracking()
            .Include(set => set.Session)
            .Where(set => set.ExerciseId == exerciseId && set.Session!.UserId == userId)
            .ToListAsync(cancellationToken);
    }
}
