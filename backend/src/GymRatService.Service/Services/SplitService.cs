using FluentValidation;
using GymRatService.Common.DTOs.Split;
using GymRatService.Common.Errors;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;
using GymRatService.Service.Validators;

namespace GymRatService.Service.Services;

public class SplitService(
    ISplitRepository splitRepository,
    IWorkoutsRepository workoutsRepository,
    IValidator<int> dayValidator) : ISplitService
{
    public async Task<IReadOnlyList<SplitDayDto>> GetSplitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var assigned = (await splitRepository.GetByUserAsync(userId, cancellationToken))
            .ToDictionary(splitDay => splitDay.Day);

        return Enumerable.Range(SplitDayValidator.FirstDay, SplitDayValidator.LastDay)
            .Select(day => new SplitDayDto(
                day,
                assigned.TryGetValue(day, out var splitDay) ? splitDay.Workout?.ToSummaryDto() : null))
            .ToList();
    }

    public async Task AssignSplitDayAsync(Guid userId, int day, AssignSplitDayRequest request, CancellationToken cancellationToken)
    {
        await dayValidator.ValidateAndThrowAsync(day, cancellationToken);

        if (request.WorkoutId is not { } workoutId)
        {
            await splitRepository.ClearDayAsync(userId, day, cancellationToken);

            return;
        }

        var workout = await workoutsRepository.GetByIdAsync(workoutId, cancellationToken);

        if (workout is null || workout.UserId != userId)
        {
            throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, workoutId);
        }

        await splitRepository.SetDayAsync(userId, day, workoutId, cancellationToken);
    }
}
