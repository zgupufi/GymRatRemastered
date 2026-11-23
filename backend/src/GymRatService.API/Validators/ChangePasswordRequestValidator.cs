using FluentValidation;
using GymRatService.Common.Constants;
using GymRatService.Common.DTOs.Users;

namespace GymRatService.API.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .MaximumLength(PasswordRules.MaximumLength);

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinimumLength)
            .MaximumLength(PasswordRules.MaximumLength)
            .NotEqual(request => request.CurrentPassword);
    }
}
