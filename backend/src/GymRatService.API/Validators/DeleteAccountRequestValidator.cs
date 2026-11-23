using FluentValidation;
using GymRatService.Common.DTOs.Users;

namespace GymRatService.API.Validators;

public class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(request => request.Password)
            .NotEmpty();
    }
}
