using FluentValidation;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;

namespace GymRatService.Service.Services;

public class WorkoutsService(
    IWorkoutsRepository workoutsRepository,
    IExerciseCardsRepository exerciseCardsRepository,
    IValidator<WorkoutFilter> filterValidator,
    IValidator<CreateWorkoutRequest> createValidator,
    IValidator<RenameWorkoutRequest> renameValidator,
    IValidator<ReplaceWorkoutExercisesRequest> replaceExercisesValidator,
    TimeProvider timeProvider) : IWorkoutsService
{
    public async Task<PagedResult<WorkoutSummaryDto>> GetWorkoutSummariesAsync(Guid userId, WorkoutFilter filter, CancellationToken cancellationToken)
    {
        await filterValidator.ValidateAndThrowAsync(filter, cancellationToken);

        var workouts = await workoutsRepository.GetByUserAsync(userId, filter, cancellationToken);

        return workouts.Map(workout => workout.ToSummaryDto());
    }

    public async Task<WorkoutDto> GetWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var workout = await GetOwnedWorkoutAsync(userId, id, cancellationToken);

        return workout.ToDto();
    }

    public async Task<CreateWorkoutResponse> CreateWorkoutAsync(Guid userId, CreateWorkoutRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var workout = new Workout
        {
            UserId = userId,
            Name = request.Name.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
        };

        await workoutsRepository.AddAsync(workout, cancellationToken);

        return new CreateWorkoutResponse(workout.Id);
    }

    public async Task RenameWorkoutAsync(Guid userId, Guid id, RenameWorkoutRequest request, CancellationToken cancellationToken)
    {
        await renameValidator.ValidateAndThrowAsync(request, cancellationToken);

        await GetOwnedWorkoutAsync(userId, id, cancellationToken);

        await workoutsRepository.RenameAsync(id, request.Name.Trim(), cancellationToken);
    }

    public async Task ReplaceWorkoutExercisesAsync(Guid userId, Guid id, ReplaceWorkoutExercisesRequest request, CancellationToken cancellationToken)
    {
        await replaceExercisesValidator.ValidateAndThrowAsync(request, cancellationToken);

        await GetOwnedWorkoutAsync(userId, id, cancellationToken);

        var exerciseIds = request.Exercises.Select(entry => entry.ExerciseId).Distinct().ToList();

        if (await exerciseCardsRepository.CountExistingAsync(exerciseIds, cancellationToken) != exerciseIds.Count)
        {
            throw new BusinessLogicException(ErrorCodes.WorkoutExerciseNotFound);
        }

        var entries = request.Exercises
            .Select((entry, index) => new WorkoutExercise
            {
                WorkoutId = id,
                ExerciseId = entry.ExerciseId,
                Position = index + 1,
                TargetSets = entry.TargetSets,
            })
            .ToList();

        await workoutsRepository.ReplaceExercisesAsync(id, entries, cancellationToken);
    }

    public async Task DeleteWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        await GetOwnedWorkoutAsync(userId, id, cancellationToken);

        await workoutsRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task<Workout> GetOwnedWorkoutAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var workout = await workoutsRepository.GetByIdAsync(id, cancellationToken);

        return workout is not null && workout.UserId == userId
            ? workout
            : throw new BusinessLogicException(ErrorCodes.WorkoutNotFound, id);
    }
}
