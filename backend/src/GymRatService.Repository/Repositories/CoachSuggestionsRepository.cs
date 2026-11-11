using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Extensions;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Repositories;

public class CoachSuggestionsRepository(GymRatDbContext context) : ICoachSuggestionsRepository
{
    public async Task AddAsync(CoachSuggestion suggestion, CancellationToken cancellationToken)
    {
        context.CoachSuggestions.Add(suggestion);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CoachSuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await WithDetails(context.CoachSuggestions.AsNoTracking())
            .FirstOrDefaultAsync(suggestion => suggestion.Id == id, cancellationToken);
    }

    public async Task<PagedResult<CoachSuggestion>> GetPendingAsync(Guid userId, CoachSuggestionFilter filter, CancellationToken cancellationToken)
    {
        var query = context.CoachSuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.UserId == userId && suggestion.Status == SuggestionStatus.Pending)
            .OrderByDirection(suggestion => suggestion.Id, filter.Sort.Direction);

        return await WithDetails(query).ToPagedResultAsync(filter.Pagination, cancellationToken);
    }

    public async Task<IReadOnlyList<CoachSuggestion>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.CoachSuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.UserId == userId && suggestion.Status == SuggestionStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task SetStatusAsync(IReadOnlyCollection<Guid> ids, SuggestionStatus status, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await context.CoachSuggestions
            .Where(suggestion => ids.Contains(suggestion.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(suggestion => suggestion.Status, status)
                    .SetProperty(suggestion => suggestion.ResolvedAt, resolvedAt),
                cancellationToken);
    }

    public async Task DismissPendingAsync(Guid userId, Guid workoutId, Guid exerciseId, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await context.CoachSuggestions
            .Where(suggestion => suggestion.UserId == userId
                && suggestion.WorkoutId == workoutId
                && suggestion.ExerciseId == exerciseId
                && suggestion.Status == SuggestionStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(suggestion => suggestion.Status, SuggestionStatus.Dismissed)
                    .SetProperty(suggestion => suggestion.ResolvedAt, resolvedAt),
                cancellationToken);
    }

    public async Task<SuggestionApplyOutcome> ApplyAsync(CoachSuggestion suggestion, WorkoutExercise settings, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await context.CoachSuggestions
            .Where(entry => entry.Id == suggestion.Id && entry.Status == SuggestionStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.Status, SuggestionStatus.Applied)
                    .SetProperty(entry => entry.ResolvedAt, resolvedAt),
                cancellationToken);

        if (claimed == 0)
        {
            return SuggestionApplyOutcome.NotPending;
        }

        var occurrences = context.WorkoutExercises
            .Where(entry => entry.WorkoutId == suggestion.WorkoutId && entry.ExerciseId == suggestion.ExerciseId);

        var total = await occurrences.CountAsync(cancellationToken);

        var updated = await occurrences
            .Where(entry => entry.TargetSets == suggestion.BeforeTargetSets
                && entry.RepsMin == suggestion.BeforeRepsMin
                && entry.RepsMax == suggestion.BeforeRepsMax
                && entry.WeightStepKg == suggestion.BeforeWeightStepKg)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.TargetSets, settings.TargetSets)
                    .SetProperty(entry => entry.RepsMin, settings.RepsMin)
                    .SetProperty(entry => entry.RepsMax, settings.RepsMax)
                    .SetProperty(entry => entry.WeightStepKg, settings.WeightStepKg),
                cancellationToken);

        if (updated != total)
        {
            return SuggestionApplyOutcome.Stale;
        }

        await transaction.CommitAsync(cancellationToken);

        return SuggestionApplyOutcome.Applied;
    }

    private static IQueryable<CoachSuggestion> WithDetails(IQueryable<CoachSuggestion> query) =>
        query
            .Include(suggestion => suggestion.Workout)
            .ThenInclude(workout => workout!.Exercises)
            .Include(suggestion => suggestion.Exercise);
}
