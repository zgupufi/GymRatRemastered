using System.Net.Mail;
using FluentValidation;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Errors;
using GymRatService.Service.Extensions;
using Microsoft.Extensions.Localization;

namespace GymRatService.Service.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private const int MinimumPasswordLength = 8;

    private const int MaximumEmailLength = 100;

    private const int MaximumNameLength = 50;

    public RegisterRequestValidator(IStringLocalizer<RegisterRequestValidator> localizer)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Email)
            .Must(BeAValidEmail)
            .WithErrorCode(ErrorCodes.InvalidEmail)
            .WithMessage(_ => localizer["InvalidEmail"]);

        RuleFor(request => request.Password)
            .MinimumLength(MinimumPasswordLength)
            .WithErrorCode(ErrorCodes.PasswordTooShort)
            .WithMessage(_ => localizer["PasswordTooShort", MinimumPasswordLength]);

        RuleFor(request => request.FirstName)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.FirstNameRequired)
            .WithMessage(_ => localizer["FirstNameRequired"])
            .Must(firstName => firstName.Trim().Length <= MaximumNameLength)
            .WithErrorCode(ErrorCodes.NameTooLong)
            .WithMessage(_ => localizer["NameTooLong", MaximumNameLength]);

        RuleFor(request => request.LastName)
            .Must(lastName => lastName is null || lastName.Trim().Length <= MaximumNameLength)
            .WithErrorCode(ErrorCodes.NameTooLong)
            .WithMessage(_ => localizer["NameTooLong", MaximumNameLength]);

        RuleFor(request => request.WeightUnit)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.InvalidWeightUnit)
            .WithMessage(_ => localizer["InvalidWeightUnit"]);
    }

    private static bool BeAValidEmail(string email)
    {
        var normalized = email.NormalizeEmail();

        return normalized.Length <= MaximumEmailLength
            && MailAddress.TryCreate(normalized, out var address)
            && address.Address == normalized;
    }
}
