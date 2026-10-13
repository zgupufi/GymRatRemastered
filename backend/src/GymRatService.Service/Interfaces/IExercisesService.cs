using GymRatService.Common.DTOs.Exercises;

namespace GymRatService.Service.Interfaces;

public interface IExercisesService
{
    Task<IReadOnlyList<ExerciseDto>> GetExercisesAsync(CancellationToken cancellationToken);

    Task<ExerciseDto> GetExerciseAsync(int id, CancellationToken cancellationToken);
}
