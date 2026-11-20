using FluentValidation;
using GymRatService.Common.Filtering;

namespace GymRatService.API.Validators;

public class ProgressFilterValidator : AbstractValidator<ProgressFilter>
{
    private const int MaximumTimeZoneLength = 100;

    public ProgressFilterValidator()
    {
        RuleFor(filter => filter.TimeZone)
            .MaximumLength(MaximumTimeZoneLength)
            .Must(BeKnownTimeZone)
            .When(filter => !string.IsNullOrEmpty(filter.TimeZone));
    }

    private static bool BeKnownTimeZone(string? timeZone)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZone!, out _);
    }
}
