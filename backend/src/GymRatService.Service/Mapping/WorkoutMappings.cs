using GymRatService.Common.DTOs.Workouts;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class WorkoutMappings
{
    public static WorkoutSummaryDto ToSummaryDto(this Workout workout) => new(
        workout.Id,
        workout.Name,
        workout.Exercises.Count,
        workout.Exercises
            .OrderBy(entry => entry.Position)
            .Select(entry => entry.Exercise?.MainMuscle)
            .OfType<string>()
            .Distinct()
            .ToList());

    public static WorkoutDto ToDto(this Workout workout) => new(
        workout.Id,
        workout.Name,
        workout.Exercises
            .OrderBy(entry => entry.Position)
            .Where(entry => entry.Exercise is not null)
            .Select((entry, index) => new WorkoutExerciseDto(index + 1, entry.TargetSets, entry.Exercise!.ToDto()))
            .ToList());
}
