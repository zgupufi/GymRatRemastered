using GymRatService.Common.Constants;
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
    IExerciseCardsRepository exerciseCardsRepository) : IWorkoutsService
{
    private const int DefaultRepsMin = 8;

    private const int DefaultRepsMax = 12;

    public async Task<PagedResult<WorkoutSummaryDto>> GetWorkoutSummariesAsync(Guid userId, WorkoutFilter filter, CancellationToken cancellationToken)
    {
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
        var workout = new Workout
        {
            UserId = userId,
            Name = request.Name.Trim(),
            CreatedAt = TimeProvider.System.GetUtcNow(),
        };

        await workoutsRepository.AddAsync(workout, cancellationToken);

        return new CreateWorkoutResponse(workout.Id);
    }

    public async Task RenameWorkoutAsync(Guid userId, Guid id, RenameWorkoutRequest request, CancellationToken cancellationToken)
    {
        await GetOwnedWorkoutAsync(userId, id, cancellationToken);

        await workoutsRepository.RenameAsync(id, request.Name.Trim(), cancellationToken);
    }

    public async Task ReplaceWorkoutExercisesAsync(Guid userId, Guid id, ReplaceWorkoutExercisesRequest request, CancellationToken cancellationToken)
    {
        var workout = await GetOwnedWorkoutAsync(userId, id, cancellationToken);

        var exerciseIds = request.Exercises.Select(entry => entry.ExerciseId).Distinct().ToList();

        var cards = await exerciseCardsRepository.GetByIdsAsync(exerciseIds, cancellationToken);

        if (cards.Count != exerciseIds.Count)
        {
            throw new BusinessLogicException(ErrorCodes.WorkoutExerciseNotFound);
        }

        var equipment = cards.ToDictionary(card => card.Id, card => card.Equipment);
        var existing = workout.Exercises
            .OrderBy(entry => entry.Position)
            .GroupBy(entry => entry.ExerciseId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var occurrences = new Dictionary<Guid, int>();

        var entries = request.Exercises
            .Select((entry, index) =>
            {
                var occurrence = occurrences.GetValueOrDefault(entry.ExerciseId);
                occurrences[entry.ExerciseId] = occurrence + 1;
                var previous = existing.GetValueOrDefault(entry.ExerciseId)?.ElementAtOrDefault(occurrence);

                return new WorkoutExercise
                {
                    WorkoutId = id,
                    ExerciseId = entry.ExerciseId,
                    Position = index + 1,
                    TargetSets = entry.TargetSets,
                    RepsMin = entry.RepsMin ?? previous?.RepsMin ?? DefaultRepsMin,
                    RepsMax = entry.RepsMax ?? previous?.RepsMax ?? DefaultRepsMax,
                    WeightStepKg = entry.WeightStepKg ?? previous?.WeightStepKg ?? WeightSteps.ForEquipment(equipment[entry.ExerciseId]),
                };
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
