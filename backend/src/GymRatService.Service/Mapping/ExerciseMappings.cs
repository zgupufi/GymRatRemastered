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

        var preferred = exercise.Translations.FirstOrDefault(entry => entry.Culture == culture);
        var fallback = exercise.Translations.FirstOrDefault(entry => entry.Culture == FallbackCulture);

        return new ExerciseDto(
            exercise.Id,
            preferred?.Name ?? fallback?.Name ?? exercise.Name,
            exercise.MainMuscle,
            exercise.SecondaryMuscles.Select(secondary => secondary.Muscle).Order().ToList(),
            exercise.Equipment,
            exercise.Difficulty,
            exercise.MovementPattern,
            preferred?.Description ?? fallback?.Description,
            preferred?.Setup ?? fallback?.Setup,
            preferred?.Execution ?? fallback?.Execution,
            exercise.YoutubeLink);
    }
}
