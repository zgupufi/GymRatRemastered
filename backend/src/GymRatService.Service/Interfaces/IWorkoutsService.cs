using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Filtering;

namespace GymRatService.Service.Interfaces;

public interface IWorkoutsService
{
    Task<PagedResult<WorkoutSummaryDto>> GetWorkoutSummariesAsync(Guid userId, WorkoutFilter filter, CancellationToken cancellationToken);

    Task<WorkoutDto> GetWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<CreateWorkoutResponse> CreateWorkoutAsync(Guid userId, CreateWorkoutRequest request, CancellationToken cancellationToken);

    Task RenameWorkoutAsync(Guid userId, Guid id, RenameWorkoutRequest request, CancellationToken cancellationToken);

    Task ReplaceWorkoutExercisesAsync(Guid userId, Guid id, ReplaceWorkoutExercisesRequest request, CancellationToken cancellationToken);

    Task DeleteWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
