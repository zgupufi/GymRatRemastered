using FluentValidation;
using GymRatService.Common.Constants;
using GymRatService.Common.DTOs.Auth;

namespace GymRatService.API.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(RegisterRequestValidator.MaximumEmailLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(PasswordRules.MaximumLength);
    }
}
