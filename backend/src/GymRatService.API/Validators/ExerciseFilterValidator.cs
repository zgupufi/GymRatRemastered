using FluentValidation;
using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;

namespace GymRatService.API.Validators;

public class ExerciseFilterValidator : FilterValidator<ExerciseFilter, ExerciseSortField>
{
    private const int MaximumMuscleLength = 50;

    public ExerciseFilterValidator()
    {
        RuleFor(filter => filter.Muscle)
            .MaximumLength(MaximumMuscleLength);

        RuleFor(filter => filter.Equipment)
            .IsInEnum();

        RuleFor(filter => filter.Pattern)
            .IsInEnum();
    }
}
