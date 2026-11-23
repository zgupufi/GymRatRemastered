using FluentValidation;
using GymRatService.Common.Constants;
using GymRatService.Common.DTOs.Auth;

namespace GymRatService.API.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    private const int MaximumEmailLength = 100;

    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(MaximumEmailLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(PasswordRules.MaximumLength);
    }
}
