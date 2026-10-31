using FluentValidation;
using GymRatService.Common.DTOs.Sessions;

namespace GymRatService.Service.Validators;

public class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(request => request.WorkoutId)
            .NotEmpty();
    }
}
