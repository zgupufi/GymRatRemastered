using System.Globalization;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class ExerciseMappings
{
    private const string FallbackCulture = "en";

    public static ExerciseDto ToDto(this ExerciseCard exercise)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        var translation = exercise.Translations.FirstOrDefault(entry => entry.Culture == culture)
            ?? exercise.Translations.FirstOrDefault(entry => entry.Culture == FallbackCulture);

        return new ExerciseDto(
            exercise.Id,
            translation?.Name ?? exercise.Name,
            exercise.MainMuscle,
            exercise.SecondaryMuscles.Select(secondary => secondary.Muscle).Order().ToList(),
            exercise.Equipment,
            exercise.Difficulty,
            exercise.MovementPattern,
            translation?.Description,
            translation?.Setup,
            translation?.Execution,
            exercise.YoutubeLink);
    }
}
