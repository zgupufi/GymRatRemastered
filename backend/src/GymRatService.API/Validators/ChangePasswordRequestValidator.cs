using FluentValidation;
using GymRatService.Common.DTOs.Users;

namespace GymRatService.API.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    private const int MinimumPasswordLength = 8;

    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty();

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength)
            .NotEqual(request => request.CurrentPassword);
    }
}
