using FluentValidation;
using GymRatService.API.Requests;
using GymRatService.Common.DTOs.Split;

namespace GymRatService.API.Validators;

public class SplitDayValidator : AbstractValidator<SplitDayRoute>
{
    public SplitDayValidator()
    {
        RuleFor(route => route.Day)
            .InclusiveBetween(SplitDays.First, SplitDays.Last);
    }
}
