using System.Globalization;
using System.Text;
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
    IExportRepository exportRepository,
    TimeProvider timeProvider) : IExportService
{
    private const string CsvHeader = "Date,Workout,Exercise,Set,Reps,Weight (kg),Reps in reserve,Target weight (kg),Target reps,Target reason,Session id";

    public async Task<UserExportDto> GetExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        var workouts = await exportRepository.GetWorkoutsAsync(userId, cancellationToken);
        var sessions = await exportRepository.GetSessionsAsync(userId, cancellationToken);
        var splitDays = await splitRepository.GetByUserAsync(userId, cancellationToken);

        var exerciseIds = workouts
            .SelectMany(workout => workout.Exercises)
            .Select(entry => entry.ExerciseId)
            .Concat(sessions.SelectMany(session => session.Sets).Select(set => set.ExerciseId))
            .ToHashSet();
        var exerciseNames = await exportRepository.GetExerciseNamesAsync(exerciseIds, cancellationToken);
        var workoutNames = workouts.ToDictionary(workout => workout.Id, workout => workout.Name);

        string NameOf(Guid exerciseId) => exerciseNames.GetValueOrDefault(exerciseId, string.Empty);

        return new UserExportDto(
            timeProvider.GetUtcNow(),
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
                        set.TargetReason))]))]);
    }

    public async Task<byte[]> GetSetsCsvAsync(Guid userId, CancellationToken cancellationToken)
    {
        var export = await GetExportAsync(userId, cancellationToken);
        var builder = new StringBuilder(CsvHeader).Append("\r\n");

        foreach (var session in export.Sessions)
        {
            foreach (var set in session.Sets)
            {
                var fields = new[]
                {
                    session.StartedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    session.WorkoutName ?? string.Empty,
                    set.ExerciseName,
                    set.SetNumber.ToString(CultureInfo.InvariantCulture),
                    set.Reps.ToString(CultureInfo.InvariantCulture),
                    FormatDecimal(set.WeightKg),
                    set.RepsInReserve?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    set.TargetWeightKg is { } targetWeight ? FormatDecimal(targetWeight) : string.Empty,
                    set.TargetReps?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    set.TargetReason?.ToString() ?? string.Empty,
                    session.Id.ToString(),
                };

                builder.AppendJoin(',', fields.Select(EscapeCsv)).Append("\r\n");
            }
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
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
