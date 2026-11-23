using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class SessionsRepository(GymRatDbContext context) : ISessionsRepository
{
    private const double EpleyRepsDivisor = 30d;

    private const double OneRepMaxTolerance = 0.0001d;

    public async Task<WorkoutSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.WorkoutSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    public async Task<WorkoutSession?> GetActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.WorkoutSessions
            .AsNoTracking()
            .Include(session => session.Sets)
            .Where(session => session.UserId == userId && session.WorkoutId != null && session.FinishedAt == null && session.Sets.Any())
            .OrderByDescending(session => session.Id)
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkoutSession?> GetOpenAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.WorkoutSessions
            .AsNoTracking()
            .Include(session => session.Sets)
            .Where(session => session.UserId == userId && session.FinishedAt == null)
            .OrderByDescending(session => session.Id)
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryAddAsync(WorkoutSession session, CancellationToken cancellationToken)
    {
        context.WorkoutSessions.Add(session);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException)
        {
            context.Entry(session).State = EntityState.Detached;

            return false;
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await context.WorkoutSessions
            .Where(session => session.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
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

    public async Task<IReadOnlyList<SessionSet>> GetBestSetPerSessionAsync(Guid userId, Guid exerciseId, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var sets = context.SessionSets
            .AsNoTracking()
            .Where(set => set.ExerciseId == exerciseId && set.Session!.UserId == userId && set.Session.FinishedAt != null);

        if (since is not null)
        {
            sets = sets.Where(set => set.Session!.StartedAt >= since);
        }

        var scored = sets.Select(set => new
        {
            set.Id,
            set.SessionId,
            OneRepMax = set.Reps == 1 ? (double)set.WeightKg : (double)set.WeightKg * (1 + set.Reps / EpleyRepsDivisor),
        });

        var bestIds = scored
            .Where(set => !scored.Any(other =>
                other.SessionId == set.SessionId && other.OneRepMax > set.OneRepMax + OneRepMaxTolerance))
            .Select(set => set.Id);

        return await context.SessionSets
            .AsNoTracking()
            .Include(set => set.Session)
            .Where(set => bestIds.Contains(set.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionSet>> GetRecentSetsAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, int sessionCount, CancellationToken cancellationToken)
    {
        var pairs = await context.SessionSets
            .AsNoTracking()
            .Where(set => exerciseIds.Contains(set.ExerciseId) && set.Session!.UserId == userId && set.Session.DurationSeconds != null)
            .Select(set => new { set.ExerciseId, set.SessionId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var recent = pairs
            .GroupBy(pair => pair.ExerciseId)
            .SelectMany(group => group.OrderByDescending(pair => pair.SessionId).Take(sessionCount))
            .ToList();

        var sessionIds = recent.Select(pair => pair.SessionId).Distinct().ToList();

        var sets = await context.SessionSets
            .AsNoTracking()
            .Include(set => set.Session)
            .Where(set => exerciseIds.Contains(set.ExerciseId) && sessionIds.Contains(set.SessionId))
            .ToListAsync(cancellationToken);

        var keys = recent.Select(pair => (pair.ExerciseId, pair.SessionId)).ToHashSet();

        return sets.Where(set => keys.Contains((set.ExerciseId, set.SessionId))).ToList();
    }

    public async Task<IReadOnlyList<SessionSet>> GetRecentExerciseSetsAsync(Guid userId, Guid exerciseId, int sessionCount, CancellationToken cancellationToken)
    {
        var sessionIds = await context.SessionSets
            .AsNoTracking()
            .Where(set => set.ExerciseId == exerciseId && set.Session!.UserId == userId && set.Session.DurationSeconds != null)
            .Select(set => set.SessionId)
            .Distinct()
            .OrderByDescending(sessionId => sessionId)
            .Take(sessionCount)
            .ToListAsync(cancellationToken);

        return await context.SessionSets
            .AsNoTracking()
            .Include(set => set.Session)
            .Where(set => set.ExerciseId == exerciseId && sessionIds.Contains(set.SessionId))
            .ToListAsync(cancellationToken);
    }
}
