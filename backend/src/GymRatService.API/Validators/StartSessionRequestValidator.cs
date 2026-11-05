using FluentValidation;
using GymRatService.Common.DTOs.Sessions;

namespace GymRatService.API.Validators;

public class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(request => request.WorkoutId)
            .NotEmpty();
    }
}
