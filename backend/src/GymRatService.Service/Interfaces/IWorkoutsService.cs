using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Service.Interfaces;

public interface IWorkoutsService
{
    Task<IReadOnlyList<WorkoutSummaryDto>> GetWorkoutSummariesAsync(Guid userId, CancellationToken cancellationToken);

    Task<WorkoutDto> GetWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<CreateWorkoutResponse> CreateWorkoutAsync(Guid userId, CreateWorkoutRequest request, CancellationToken cancellationToken);

    Task RenameWorkoutAsync(Guid userId, Guid id, RenameWorkoutRequest request, CancellationToken cancellationToken);

    Task ReplaceWorkoutExercisesAsync(Guid userId, Guid id, ReplaceWorkoutExercisesRequest request, CancellationToken cancellationToken);

    Task DeleteWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
