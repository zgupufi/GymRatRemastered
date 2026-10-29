using FluentValidation;
using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;

namespace GymRatService.Service.Validators;

public class ExerciseFilterValidator : FilterValidator<ExerciseFilter, ExerciseSortField>
{
    private const int MaximumMuscleLength = 50;

    public ExerciseFilterValidator()
    {
        RuleFor(filter => filter.Muscle)
            .MaximumLength(MaximumMuscleLength);
    }
}
