using FluentValidation;
using GymRatService.Common.DTOs.Users;

namespace GymRatService.API.Validators;

public class UpdateUserSettingsRequestValidator : AbstractValidator<UpdateUserSettingsRequest>
{
    private const int MinimumRestTimeSeconds = 30;

    private const int MaximumRestTimeSeconds = 600;

    public UpdateUserSettingsRequestValidator()
    {
        RuleFor(request => request.WeightUnit)
            .IsInEnum();

        RuleFor(request => request.DefaultRestTimeSeconds)
            .InclusiveBetween(MinimumRestTimeSeconds, MaximumRestTimeSeconds);
    }
}
