using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Service.Validators;

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
