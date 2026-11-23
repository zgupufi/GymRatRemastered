using GymRatService.Common.DTOs.Export;
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

    public async Task<IReadOnlyList<CoachSuggestion>> GetCoachSuggestionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.CoachSuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.UserId == userId)
            .OrderBy(suggestion => suggestion.Id)
            .ToListAsync(cancellationToken);
    }

    public IAsyncEnumerable<ExportSetRowDto> StreamSetRows(Guid userId)
    {
        return context.SessionSets
            .AsNoTracking()
            .Where(set => set.Session!.UserId == userId)
            .Join(context.Exercises, set => set.ExerciseId, exercise => exercise.Id, (set, exercise) => new { Set = set, ExerciseName = exercise.Name })
            .OrderBy(entry => entry.Set.SessionId)
            .ThenBy(entry => entry.ExerciseName)
            .ThenBy(entry => entry.Set.SetNumber)
            .Select(entry => new ExportSetRowDto(
                entry.Set.SessionId,
                entry.Set.Session!.StartedAt,
                context.Workouts.Where(workout => workout.Id == entry.Set.Session.WorkoutId).Select(workout => workout.Name).FirstOrDefault(),
                entry.ExerciseName,
                entry.Set.SetNumber,
                entry.Set.Reps,
                entry.Set.WeightKg,
                entry.Set.RepsInReserve,
                entry.Set.TargetWeightKg,
                entry.Set.TargetReps,
                entry.Set.TargetReason))
            .AsAsyncEnumerable();
    }
}
