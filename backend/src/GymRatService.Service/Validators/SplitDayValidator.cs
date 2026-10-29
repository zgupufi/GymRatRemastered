using FluentValidation;

namespace GymRatService.Service.Validators;

public class SplitDayValidator : AbstractValidator<int>
{
    public const int FirstDay = 1;

    public const int LastDay = 7;

    public SplitDayValidator()
    {
        RuleFor(day => day)
            .InclusiveBetween(FirstDay, LastDay);
    }
}
