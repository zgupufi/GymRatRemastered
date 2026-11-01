using FluentValidation;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Filtering;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using Microsoft.Extensions.AI;

namespace GymRatService.Service.Services;

public sealed class ChatTools(
    Guid userId,
    IWorkoutsService workoutsService,
    ISplitService splitService,
    IExercisesService exercisesService,
    CancellationToken cancellationToken)
{
    private const int SearchResultLimit = 20;

    public bool WorkoutsChanged { get; private set; }

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(ListWorkoutsAsync, "list_workouts", "Lists the workouts of the user with their main muscles."),
        AIFunctionFactory.Create(GetWorkoutAsync, "get_workout", "Gets one workout of the user with its exercises."),
        AIFunctionFactory.Create(CreateWorkoutAsync, "create_workout", "Creates a workout for the user, optionally with exercises."),
        AIFunctionFactory.Create(UpdateWorkoutAsync, "update_workout", "Renames a workout and/or replaces all of its exercises."),
        AIFunctionFactory.Create(DeleteWorkoutAsync, "delete_workout", "Deletes a workout of the user."),
        AIFunctionFactory.Create(ReadSplitAsync, "read_split", "Reads the weekly training split of the user, days 1 to 7."),
        AIFunctionFactory.Create(SearchExercisesAsync, "search_exercises", "Searches the exercise catalog by name and/or muscle."),
    ];

    private Task<object> ListWorkoutsAsync() => RunAsync(async () =>
    {
        var filter = new WorkoutFilter { Pagination = new PaginationFilter { PageSize = PaginationFilter.MaximumPageSize } };

        return (await workoutsService.GetWorkoutSummariesAsync(userId, filter, cancellationToken)).Items;
    });

    private Task<object> GetWorkoutAsync(Guid workoutId) => RunAsync(async () =>
        await workoutsService.GetWorkoutAsync(userId, workoutId, cancellationToken));

    private Task<object> CreateWorkoutAsync(string name, IReadOnlyList<WorkoutExerciseRequest>? exercises = null) => RunAsync(async () =>
    {
        var created = await workoutsService.CreateWorkoutAsync(userId, new CreateWorkoutRequest(name), cancellationToken);
        WorkoutsChanged = true;

        if (exercises is { Count: > 0 })
        {
            await workoutsService.ReplaceWorkoutExercisesAsync(userId, created.Id, new ReplaceWorkoutExercisesRequest(exercises), cancellationToken);
        }

        return created;
    });

    private Task<object> UpdateWorkoutAsync(Guid workoutId, string? name = null, IReadOnlyList<WorkoutExerciseRequest>? exercises = null) => RunAsync(async () =>
    {
        if (name is not null)
        {
            await workoutsService.RenameWorkoutAsync(userId, workoutId, new RenameWorkoutRequest(name), cancellationToken);
            WorkoutsChanged = true;
        }

        if (exercises is not null)
        {
            await workoutsService.ReplaceWorkoutExercisesAsync(userId, workoutId, new ReplaceWorkoutExercisesRequest(exercises), cancellationToken);
            WorkoutsChanged = true;
        }

        return new { updated = true };
    });

    private Task<object> DeleteWorkoutAsync(Guid workoutId) => RunAsync(async () =>
    {
        await workoutsService.DeleteWorkoutAsync(userId, workoutId, cancellationToken);
        WorkoutsChanged = true;

        return new { deleted = true };
    });

    private Task<object> ReadSplitAsync() => RunAsync(async () =>
        await splitService.GetSplitAsync(userId, cancellationToken));

    private Task<object> SearchExercisesAsync(string? term = null, string? muscle = null) => RunAsync(async () =>
    {
        var filter = new ExerciseFilter
        {
            Muscle = muscle,
            Search = new SearchFilter { Term = term },
            Pagination = new PaginationFilter { PageSize = SearchResultLimit },
        };

        return (await exercisesService.GetExercisesAsync(filter, cancellationToken)).Items;
    });

    private static async Task<object> RunAsync(Func<Task<object>> action)
    {
        try
        {
            return await action();
        }
        catch (BusinessLogicException exception)
        {
            return new { error = exception.Code };
        }
        catch (ValidationException exception)
        {
            return new { error = exception.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}") };
        }
    }
}
