using GymRatService.Common.Constants;

namespace GymRatService.API.Validators;

public static class ExerciseSettingsRules
{
    public const int MinimumTargetSets = 1;

    public const int MaximumTargetSets = 20;

    public const int MinimumReps = 1;

    public const int MaximumReps = 30;

    public static bool IsAllowedStep(decimal? weightStepKg) => weightStepKg is null || WeightSteps.Allowed.Contains(weightStepKg.Value);
}
