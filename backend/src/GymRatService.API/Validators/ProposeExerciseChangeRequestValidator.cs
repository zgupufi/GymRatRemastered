using FluentValidation;
using GymRatService.Common.DTOs.Coach;

namespace GymRatService.API.Validators;

public class ProposeExerciseChangeRequestValidator : AbstractValidator<ProposeExerciseChangeRequest>
{
    public ProposeExerciseChangeRequestValidator()
    {
        RuleFor(request => request.WorkoutId)
            .NotEmpty();

        RuleFor(request => request.ExerciseId)
            .NotEmpty();

        RuleFor(request => request.Rationale)
            .NotEmpty();

        RuleFor(request => request.TargetSets)
            .InclusiveBetween(ExerciseSettingsRules.MinimumTargetSets, ExerciseSettingsRules.MaximumTargetSets)
            .When(request => request.TargetSets is not null);

        RuleFor(request => request.RepsMin)
            .InclusiveBetween(ExerciseSettingsRules.MinimumReps, ExerciseSettingsRules.MaximumReps)
            .When(request => request.RepsMin is not null);

        RuleFor(request => request.RepsMax)
            .InclusiveBetween(ExerciseSettingsRules.MinimumReps, ExerciseSettingsRules.MaximumReps)
            .When(request => request.RepsMax is not null);

        RuleFor(request => request.WeightStepKg)
            .Must(ExerciseSettingsRules.IsAllowedStep);

        RuleFor(request => request)
            .Must(request => request.TargetSets is not null || request.RepsMin is not null || request.RepsMax is not null || request.WeightStepKg is not null);
    }
}
