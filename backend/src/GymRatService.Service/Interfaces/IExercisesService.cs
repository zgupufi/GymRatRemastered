using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Filtering;

namespace GymRatService.Service.Interfaces;

public interface IExercisesService
{
    Task<PagedResult<ExerciseDto>> GetExercisesAsync(ExerciseFilter filter, CancellationToken cancellationToken);

    Task<ExerciseDto> GetExerciseAsync(Guid id, CancellationToken cancellationToken);
}
