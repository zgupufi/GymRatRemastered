using GymRatService.Common.DTOs.Sessions;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class SessionMappings
{
    public static SessionSetDto ToDto(this SessionSet set) => new(set.SetNumber, set.Reps, set.WeightKg);

    public static LastSessionDto ToLastSessionDto(this WorkoutSession session) => new(
        session.Id,
        session.StartedAt,
        session.DurationSeconds,
        session.Sets
            .GroupBy(set => set.ExerciseId)
            .Select(group => new LastSessionExerciseDto(
                group.Key,
                group.OrderBy(set => set.SetNumber).Select(set => set.ToDto()).ToList()))
            .ToList());
}
