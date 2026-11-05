using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.API.Validators;

public class RenameWorkoutRequestValidator : AbstractValidator<RenameWorkoutRequest>
{
    public RenameWorkoutRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CreateWorkoutRequestValidator.MaximumNameLength);
    }
}
