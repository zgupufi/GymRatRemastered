using FluentValidation;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;

namespace GymRatService.Service.Services;

public class ExercisesService(
    IExerciseCardsRepository exerciseCardsRepository,
    IValidator<ExerciseFilter> filterValidator) : IExercisesService
{
    public async Task<PagedResult<ExerciseDto>> GetExercisesAsync(ExerciseFilter filter, CancellationToken cancellationToken)
    {
        await filterValidator.ValidateAndThrowAsync(filter, cancellationToken);

        var exercises = await exerciseCardsRepository.GetAllAsync(filter, cancellationToken);

        return exercises.Map(exercise => exercise.ToDto());
    }

    public async Task<ExerciseDto> GetExerciseAsync(Guid id, CancellationToken cancellationToken)
    {
        var exercise = await exerciseCardsRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.ExerciseNotFound, id);

        return exercise.ToDto();
    }
}
