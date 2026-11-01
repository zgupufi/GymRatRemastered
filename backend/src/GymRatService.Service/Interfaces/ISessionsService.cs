using GymRatService.Common.DTOs.Sessions;

namespace GymRatService.Service.Interfaces;

public interface ISessionsService
{
    Task<StartSessionResponse> StartSessionAsync(Guid userId, StartSessionRequest request, CancellationToken cancellationToken);

    Task SaveExerciseSetsAsync(Guid userId, Guid id, Guid exerciseId, SaveExerciseSetsRequest request, CancellationToken cancellationToken);

    Task FinishSessionAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<LastSessionDto> GetLastSessionAsync(Guid userId, Guid workoutId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExerciseProgressDto>> GetExerciseProgressAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken);
}
