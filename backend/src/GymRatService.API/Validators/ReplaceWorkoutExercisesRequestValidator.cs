using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.API.Validators;

public class ReplaceWorkoutExercisesRequestValidator : AbstractValidator<ReplaceWorkoutExercisesRequest>
{
    private const int MaximumExercises = 50;

    public ReplaceWorkoutExercisesRequestValidator()
    {
        RuleFor(request => request.Exercises)
            .NotNull();

        RuleFor(request => request.Exercises)
            .Must(exercises => exercises is not { Count: > MaximumExercises });

        RuleFor(request => request.Exercises)
            .Must(exercises => exercises.Select(exercise => exercise.ExerciseId).Distinct().Count() == exercises.Count)
            .When(request => request.Exercises is not null);

        RuleForEach(request => request.Exercises)
            .SetValidator(new WorkoutExerciseRequestValidator());
    }
}
