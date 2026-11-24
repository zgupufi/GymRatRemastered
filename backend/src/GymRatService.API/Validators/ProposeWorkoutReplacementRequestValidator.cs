using FluentValidation;
using GymRatService.Common.DTOs.Coach;

namespace GymRatService.API.Validators;

public class ProposeWorkoutReplacementRequestValidator : AbstractValidator<ProposeWorkoutReplacementRequest>
{
    private const int MaximumExercises = 50;

    public ProposeWorkoutReplacementRequestValidator()
    {
        RuleFor(request => request.WorkoutId)
            .NotEmpty();

        RuleFor(request => request.Rationale)
            .NotEmpty();

        RuleFor(request => request.Exercises)
            .NotNull();

        RuleFor(request => request.Exercises.Count)
            .LessThanOrEqualTo(MaximumExercises)
            .When(request => request.Exercises is not null);

        RuleFor(request => request.Exercises)
            .Must(exercises => exercises.Select(exercise => exercise.ExerciseId).Distinct().Count() == exercises.Count)
            .When(request => request.Exercises is not null);

        RuleForEach(request => request.Exercises)
            .SetValidator(new WorkoutExerciseRequestValidator());
    }
}
