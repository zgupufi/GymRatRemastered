using FluentValidation;
using GymRatService.Common.Constants;
using GymRatService.Common.DTOs.Auth;

namespace GymRatService.API.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MaximumEmailLength = 100;

    private const int MaximumNameLength = 50;

    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(MaximumEmailLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinimumLength)
            .MaximumLength(PasswordRules.MaximumLength);

        RuleFor(request => request.FirstName)
            .NotEmpty()
            .MaximumLength(MaximumNameLength);

        RuleFor(request => request.LastName)
            .MaximumLength(MaximumNameLength);

        RuleFor(request => request.WeightUnit)
            .IsInEnum();
    }
}
