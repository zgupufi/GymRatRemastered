using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Service.Validators;

public class CreateWorkoutRequestValidator : AbstractValidator<CreateWorkoutRequest>
{
    public const int MaximumNameLength = 100;

    public CreateWorkoutRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(MaximumNameLength);
    }
}
