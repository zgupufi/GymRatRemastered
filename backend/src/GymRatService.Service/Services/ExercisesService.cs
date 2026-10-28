using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;

namespace GymRatService.Service.Services;

public class ExercisesService(IExerciseCardsRepository exerciseCardsRepository) : IExercisesService
{
    public async Task<IReadOnlyList<ExerciseDto>> GetExercisesAsync(CancellationToken cancellationToken)
    {
        var exercises = await exerciseCardsRepository.GetAllAsync(cancellationToken);

        return exercises.Select(exercise => exercise.ToDto()).ToList();
    }

    public async Task<ExerciseDto> GetExerciseAsync(Guid id, CancellationToken cancellationToken)
    {
        var exercise = await exerciseCardsRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.ExerciseNotFound, id);

        return exercise.ToDto();
    }
}
