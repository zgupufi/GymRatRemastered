using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;

namespace GymRatService.Repository.Interfaces;

public interface ICoachSuggestionsRepository
{
    Task AddAsync(CoachSuggestion suggestion, CancellationToken cancellationToken);

    Task<CoachSuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<CoachSuggestion>> GetPendingAsync(Guid userId, CoachSuggestionFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<CoachSuggestion>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task SetStatusAsync(IReadOnlyCollection<Guid> ids, SuggestionStatus status, DateTimeOffset resolvedAt, CancellationToken cancellationToken);

    Task DismissPendingAsync(Guid userId, Guid workoutId, Guid exerciseId, DateTimeOffset resolvedAt, CancellationToken cancellationToken);

    Task<SuggestionApplyOutcome> ApplyAsync(CoachSuggestion suggestion, WorkoutExercise settings, DateTimeOffset resolvedAt, CancellationToken cancellationToken);
}
