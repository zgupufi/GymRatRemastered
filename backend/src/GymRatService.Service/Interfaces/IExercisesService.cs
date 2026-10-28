using GymRatService.Common.DTOs.Exercises;

namespace GymRatService.Service.Interfaces;

public interface IExercisesService
{
    Task<IReadOnlyList<ExerciseDto>> GetExercisesAsync(string? search, string? muscle, CancellationToken cancellationToken);

    Task<ExerciseDto> GetExerciseAsync(Guid id, CancellationToken cancellationToken);
}
