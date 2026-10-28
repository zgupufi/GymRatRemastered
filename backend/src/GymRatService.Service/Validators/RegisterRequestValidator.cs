using FluentValidation;
using GymRatService.Common.DTOs.Auth;

namespace GymRatService.Service.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private const int MinimumPasswordLength = 8;

    private const int MaximumEmailLength = 100;

    private const int MaximumNameLength = 50;

    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(MaximumEmailLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength);

        RuleFor(request => request.FirstName)
            .NotEmpty()
            .MaximumLength(MaximumNameLength);

        RuleFor(request => request.LastName)
            .MaximumLength(MaximumNameLength);

        RuleFor(request => request.WeightUnit)
            .IsInEnum();
    }
}
