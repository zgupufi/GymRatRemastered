using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.API.Validators;

public class WorkoutExerciseRequestValidator : AbstractValidator<WorkoutExerciseRequest>
{
    public WorkoutExerciseRequestValidator()
    {
        RuleFor(request => request.ExerciseId)
            .NotEmpty();

        RuleFor(request => request.TargetSets)
            .InclusiveBetween(ExerciseSettingsRules.MinimumTargetSets, ExerciseSettingsRules.MaximumTargetSets);

        RuleFor(request => request.RepsMin)
            .InclusiveBetween(ExerciseSettingsRules.MinimumReps, ExerciseSettingsRules.MaximumReps)
            .When(request => request.RepsMin is not null);

        RuleFor(request => request.RepsMax)
            .InclusiveBetween(ExerciseSettingsRules.MinimumReps, ExerciseSettingsRules.MaximumReps)
            .When(request => request.RepsMax is not null);

        RuleFor(request => request.RepsMax)
            .GreaterThanOrEqualTo(request => request.RepsMin)
            .When(request => request.RepsMin is not null && request.RepsMax is not null);

        RuleFor(request => request.RepsMax)
            .NotNull()
            .When(request => request.RepsMin is not null);

        RuleFor(request => request.RepsMin)
            .NotNull()
            .When(request => request.RepsMax is not null);

        RuleFor(request => request.WeightStepKg)
            .Must(ExerciseSettingsRules.IsAllowedStep);
    }
}
