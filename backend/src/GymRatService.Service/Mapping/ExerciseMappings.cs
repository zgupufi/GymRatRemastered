using GymRatService.Common.DTOs.Exercises;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class ExerciseMappings
{
    public static ExerciseDto ToDto(this ExerciseCard exercise) => new(
        exercise.Id,
        exercise.Name,
        exercise.MainMuscle,
        exercise.SubMuscles,
        exercise.Equipment,
        exercise.Difficulty,
        exercise.Description,
        exercise.YoutubeLink);
}
