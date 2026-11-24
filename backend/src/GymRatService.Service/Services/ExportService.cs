using System.Globalization;
using System.Runtime.CompilerServices;
using GymRatService.Common.DTOs.Export;
using GymRatService.Common.Errors;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;

namespace GymRatService.Service.Services;

public class ExportService(
    IUsersRepository usersRepository,
    ISplitRepository splitRepository,
    IExportRepository exportRepository) : IExportService
{
    private const string CsvHeader = "Date,Workout,Exercise,Set,Reps,Weight (kg),Reps in reserve,Target weight (kg),Target reps,Target reason,Session id";

    public async Task<UserExportDto> GetExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        var workouts = await exportRepository.GetWorkoutsAsync(userId, cancellationToken);
        var sessions = await exportRepository.GetSessionsAsync(userId, cancellationToken);
        var coachSuggestions = await exportRepository.GetCoachSuggestionsAsync(userId, cancellationToken);
        var splitDays = await splitRepository.GetByUserAsync(userId, cancellationToken);

        var exerciseIds = workouts
            .SelectMany(workout => workout.Exercises)
            .Select(entry => entry.ExerciseId)
            .Concat(sessions.SelectMany(session => session.Sets).Select(set => set.ExerciseId))
            .Concat(coachSuggestions
                .Where(suggestion => suggestion.ExerciseId.HasValue)
                .Select(suggestion => suggestion.ExerciseId!.Value))
            .ToHashSet();
        var exerciseNames = await exportRepository.GetExerciseNamesAsync(exerciseIds, cancellationToken);
        var workoutNames = workouts.ToDictionary(workout => workout.Id, workout => workout.Name);

        string NameOf(Guid exerciseId) => exerciseNames.GetValueOrDefault(exerciseId, string.Empty);

        return new UserExportDto(
            TimeProvider.System.GetUtcNow(),
            user.ToProfileDto(),
            [.. workouts.Select(workout => new ExportWorkoutDto(
                workout.Id,
                workout.Name,
                workout.CreatedAt,
                [.. workout.Exercises
                    .OrderBy(entry => entry.Position)
                    .Select(entry => new ExportWorkoutExerciseDto(
                        entry.ExerciseId,
                        NameOf(entry.ExerciseId),
                        entry.Position,
                        entry.TargetSets,
                        entry.RepsMin,
                        entry.RepsMax,
                        entry.WeightStepKg))])),
            ],
            [.. splitDays.Select(splitDay => new ExportSplitDayDto(
                splitDay.Day,
                splitDay.WorkoutId,
                workoutNames.GetValueOrDefault(splitDay.WorkoutId, string.Empty)))],
            [.. sessions.Select(session => new ExportSessionDto(
                session.Id,
                session.WorkoutId,
                session.WorkoutId is { } workoutId ? workoutNames.GetValueOrDefault(workoutId) : null,
                session.StartedAt,
                session.FinishedAt,
                session.DurationSeconds,
                [.. session.Sets
                    .OrderBy(set => NameOf(set.ExerciseId), StringComparer.Ordinal)
                    .ThenBy(set => set.SetNumber)
                    .Select(set => new ExportSetDto(
                        set.ExerciseId,
                        NameOf(set.ExerciseId),
                        set.SetNumber,
                        set.Reps,
                        set.WeightKg,
                        set.RepsInReserve,
                        set.TargetWeightKg,
                        set.TargetReps,
                        set.TargetReason))]))],
            [.. coachSuggestions.Select(suggestion => new ExportCoachSuggestionDto(
                suggestion.Id,
                suggestion.Kind,
                suggestion.WorkoutId,
                workoutNames.GetValueOrDefault(suggestion.WorkoutId, string.Empty),
                suggestion.ExerciseId,
                suggestion.ExerciseId is { } suggestionExerciseId ? NameOf(suggestionExerciseId) : string.Empty,
                suggestion.TargetSets,
                suggestion.RepsMin,
                suggestion.RepsMax,
                suggestion.WeightStepKg,
                suggestion.BeforeTargetSets,
                suggestion.BeforeRepsMin,
                suggestion.BeforeRepsMax,
                suggestion.BeforeWeightStepKg,
                suggestion.Rationale,
                suggestion.Status,
                suggestion.CreatedAt,
                suggestion.ResolvedAt))]);
    }

    public async Task<IAsyncEnumerable<string>> GetSetsCsvLinesAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        return EnumerateSetsCsvLinesAsync(userId, cancellationToken);
    }

    private async IAsyncEnumerable<string> EnumerateSetsCsvLinesAsync(Guid userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return CsvHeader;

        await foreach (var row in exportRepository.StreamSetRows(userId).WithCancellation(cancellationToken))
        {
            string[] fields =
            [
                row.StartedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                row.WorkoutName ?? string.Empty,
                row.ExerciseName,
                row.SetNumber.ToString(CultureInfo.InvariantCulture),
                row.Reps.ToString(CultureInfo.InvariantCulture),
                FormatDecimal(row.WeightKg),
                row.RepsInReserve?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.TargetWeightKg is { } targetWeight ? FormatDecimal(targetWeight) : string.Empty,
                row.TargetReps?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.TargetReason?.ToString() ?? string.Empty,
                row.SessionId.ToString(),
            ];

            yield return string.Join(',', fields.Select(EscapeCsv));
        }
    }

    private static string FormatDecimal(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
