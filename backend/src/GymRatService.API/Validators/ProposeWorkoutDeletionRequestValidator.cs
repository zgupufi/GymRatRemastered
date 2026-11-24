using FluentValidation;
using GymRatService.Common.DTOs.Coach;

namespace GymRatService.API.Validators;

public class ProposeWorkoutDeletionRequestValidator : AbstractValidator<ProposeWorkoutDeletionRequest>
{
    public ProposeWorkoutDeletionRequestValidator()
    {
        RuleFor(request => request.WorkoutId)
            .NotEmpty();

        RuleFor(request => request.Rationale)
            .NotEmpty();
    }
}
