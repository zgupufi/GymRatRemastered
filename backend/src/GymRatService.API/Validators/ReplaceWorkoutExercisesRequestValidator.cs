using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.API.Validators;

public class ReplaceWorkoutExercisesRequestValidator : AbstractValidator<ReplaceWorkoutExercisesRequest>
{
    public ReplaceWorkoutExercisesRequestValidator()
    {
        RuleFor(request => request.Exercises)
            .NotNull();

        RuleForEach(request => request.Exercises)
            .SetValidator(new WorkoutExerciseRequestValidator());
    }
}
