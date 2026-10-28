using FluentValidation;
using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Service.Validators;

public class RenameWorkoutRequestValidator : AbstractValidator<RenameWorkoutRequest>
{
    public RenameWorkoutRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CreateWorkoutRequestValidator.MaximumNameLength);
    }
}
