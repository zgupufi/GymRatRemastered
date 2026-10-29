using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Service.Validators;

public class WorkoutExerciseRequestValidator : AbstractValidator<WorkoutExerciseRequest>
{
    private const int MinimumTargetSets = 1;

    private const int MaximumTargetSets = 20;

    public WorkoutExerciseRequestValidator()
    {
        RuleFor(request => request.ExerciseId)
            .NotEmpty();

        RuleFor(request => request.TargetSets)
            .InclusiveBetween(MinimumTargetSets, MaximumTargetSets);
    }
}
