using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Targets;

namespace GymRatService.Service.Interfaces;

public interface ISessionsService
{
    Task<StartSessionResponse> StartSessionAsync(Guid userId, StartSessionRequest request, CancellationToken cancellationToken);

    Task<ActiveSessionDto> GetActiveSessionAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveExerciseSetsAsync(Guid userId, Guid id, Guid exerciseId, SaveExerciseSetsRequest request, CancellationToken cancellationToken);

    Task FinishSessionAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<LastSessionDto> GetLastSessionAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExerciseProgressDto>> GetExerciseProgressAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExerciseTargetDto>> GetTargetsAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExerciseHistoryDto>> GetExerciseHistoryAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken);
}
