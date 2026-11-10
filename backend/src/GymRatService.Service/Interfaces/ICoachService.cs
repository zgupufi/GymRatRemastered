using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.Filtering;

namespace GymRatService.Service.Interfaces;

public interface ICoachService
{
    Task<PagedResult<CoachSuggestionDto>> GetPendingSuggestionsAsync(Guid userId, CoachSuggestionFilter filter, CancellationToken cancellationToken);

    Task<CoachSuggestionDto> ProposeExerciseChangeAsync(Guid userId, ProposeExerciseChangeRequest request, CancellationToken cancellationToken);

    Task ApplySuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task DismissSuggestionAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
