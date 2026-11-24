using FluentValidation;
using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Errors;
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
    ISessionsService sessionsService,
    ICoachService coachService,
    IInputValidator inputValidator,
    CancellationToken cancellationToken)
{
    private const int SearchResultLimit = 20;

    private const int MaximumProposalsPerTurn = 3;

    private readonly List<CoachSuggestionDto> _suggestions = [];

    private int _proposalCount;

    public bool WorkoutsChanged { get; private set; }

    public IReadOnlyList<CoachSuggestionDto> Suggestions => _suggestions;

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(ListWorkoutsAsync, "list_workouts", "Lists the workouts of the user with their main muscles."),
        AIFunctionFactory.Create(GetWorkoutAsync, "get_workout", "Gets one workout of the user with its exercises."),
        AIFunctionFactory.Create(CreateWorkoutAsync, "create_workout", "Creates a workout for the user, optionally with exercises."),
        AIFunctionFactory.Create(RenameWorkoutAsync, "rename_workout", "Renames a workout of the user."),
        AIFunctionFactory.Create(ReadSplitAsync, "read_split", "Reads the weekly training split of the user, days 1 to 7."),
        AIFunctionFactory.Create(SearchExercisesAsync, "search_exercises", "Searches the exercise catalog by name and/or muscle."),
        AIFunctionFactory.Create(GetExerciseHistoryAsync, "get_exercise_history", "Gets the last sessions of one exercise with reps, weight in kg and reps in reserve (0 = max effort)."),
        AIFunctionFactory.Create(GetTargetsAsync, "get_targets", "Gets today's target per exercise of a workout: weight, reps, rep range and reason (Hold, Stalled, IncreaseWeight, AddRep, NoHistory)."),
        AIFunctionFactory.Create(
            ProposeExerciseChangeAsync,
            "propose_exercise_change",
            "Proposes new target sets (at most 1 more or fewer), rep range (1 to 30) and/or weight step in kg (1, 1.25, 2, 2.5 or 5) for one exercise in a workout of the user, with a short rationale. The user confirms it before anything changes."),
        AIFunctionFactory.Create(
            ProposeWorkoutReplacementAsync,
            "propose_workout_replacement",
            "Proposes replacing the whole exercise list of an existing workout of the user with the given exercises, in order, each with target sets, and a short rationale. The workout keeps its name and nothing changes until the user confirms it."),
        AIFunctionFactory.Create(
            ProposeWorkoutDeletionAsync,
            "propose_workout_deletion",
            "Proposes deleting a workout of the user with a short rationale. The workout is only deleted when the user confirms it."),
    ];

    private Task<object> GetExerciseHistoryAsync(Guid exerciseId) => RunAsync(async () =>
        await sessionsService.GetExerciseHistoryAsync(userId, exerciseId, cancellationToken));

    private Task<object> GetTargetsAsync(Guid workoutId) => RunAsync(async () =>
        await sessionsService.GetTargetsAsync(userId, workoutId, cancellationToken));

    private Task<object> ProposeExerciseChangeAsync(
        Guid workoutId,
        Guid exerciseId,
        string rationale,
        int? targetSets = null,
        int? repsMin = null,
        int? repsMax = null,
        decimal? weightStepKg = null) => RunAsync(async () =>
    {
        ReserveProposal();

        var request = new ProposeExerciseChangeRequest(workoutId, exerciseId, targetSets, repsMin, repsMax, weightStepKg, rationale);
        await inputValidator.ValidateAsync(request, cancellationToken);

        var suggestion = await coachService.ProposeExerciseChangeAsync(userId, request, cancellationToken);
        AddSuggestion(suggestion, existing => existing.Kind == suggestion.Kind && existing.WorkoutId == suggestion.WorkoutId && existing.ExerciseId == suggestion.ExerciseId);

        return new { proposed = true, suggestion.Id };
    });

    private Task<object> ProposeWorkoutReplacementAsync(Guid workoutId, IReadOnlyList<WorkoutExerciseRequest> exercises, string rationale) => RunAsync(async () =>
    {
        ReserveProposal();

        var request = new ProposeWorkoutReplacementRequest(workoutId, exercises, rationale);
        await inputValidator.ValidateAsync(request, cancellationToken);

        var suggestion = await coachService.ProposeWorkoutReplacementAsync(userId, request, cancellationToken);
        AddSuggestion(suggestion, existing => existing.Kind == suggestion.Kind && existing.WorkoutId == suggestion.WorkoutId);

        return new { proposed = true, suggestion.Id };
    });

    private Task<object> ProposeWorkoutDeletionAsync(Guid workoutId, string rationale) => RunAsync(async () =>
    {
        ReserveProposal();

        var request = new ProposeWorkoutDeletionRequest(workoutId, rationale);
        await inputValidator.ValidateAsync(request, cancellationToken);

        var suggestion = await coachService.ProposeWorkoutDeletionAsync(userId, request, cancellationToken);
        AddSuggestion(suggestion, existing => existing.Kind == suggestion.Kind && existing.WorkoutId == suggestion.WorkoutId);

        return new { proposed = true, suggestion.Id };
    });

    private void ReserveProposal()
    {
        if (_proposalCount >= MaximumProposalsPerTurn)
        {
            throw new BusinessLogicException(ErrorCodes.CoachTooManyProposals);
        }

        _proposalCount++;
    }

    private void AddSuggestion(CoachSuggestionDto suggestion, Predicate<CoachSuggestionDto> replaces)
    {
        _suggestions.RemoveAll(replaces);
        _suggestions.Add(suggestion);
    }

    private Task<object> ListWorkoutsAsync() => RunAsync(async () =>
    {
        var filter = new WorkoutFilter { Pagination = new PaginationFilter { PageSize = PaginationFilter.MaximumPageSize } };

        await inputValidator.ValidateAsync(filter, cancellationToken);

        return (await workoutsService.GetWorkoutSummariesAsync(userId, filter, cancellationToken)).Items;
    });

    private Task<object> GetWorkoutAsync(Guid workoutId) => RunAsync(async () =>
        await workoutsService.GetWorkoutAsync(userId, workoutId, cancellationToken));

    private Task<object> CreateWorkoutAsync(string name, IReadOnlyList<WorkoutExerciseRequest>? exercises = null) => RunAsync(async () =>
    {
        var createRequest = new CreateWorkoutRequest(name);
        await inputValidator.ValidateAsync(createRequest, cancellationToken);

        var created = await workoutsService.CreateWorkoutAsync(userId, createRequest, cancellationToken);
        WorkoutsChanged = true;

        if (exercises is { Count: > 0 })
        {
            var replaceRequest = new ReplaceWorkoutExercisesRequest(exercises);
            await inputValidator.ValidateAsync(replaceRequest, cancellationToken);
            await workoutsService.ReplaceWorkoutExercisesAsync(userId, created.Id, replaceRequest, cancellationToken);
        }

        return created;
    });

    private Task<object> RenameWorkoutAsync(Guid workoutId, string name) => RunAsync(async () =>
    {
        var renameRequest = new RenameWorkoutRequest(name);
        await inputValidator.ValidateAsync(renameRequest, cancellationToken);
        await workoutsService.RenameWorkoutAsync(userId, workoutId, renameRequest, cancellationToken);
        WorkoutsChanged = true;

        return new { renamed = true };
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

        await inputValidator.ValidateAsync(filter, cancellationToken);

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
