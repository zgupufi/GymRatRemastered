using FluentValidation;
using GymRatService.Common.DTOs.Sessions;

namespace GymRatService.Service.Validators;

public class SaveExerciseSetsRequestValidator : AbstractValidator<SaveExerciseSetsRequest>
{
    private const int MaximumSets = 50;

    public SaveExerciseSetsRequestValidator()
    {
        RuleFor(request => request.Sets)
            .NotNull()
            .Must(sets => sets is null || sets.Count <= MaximumSets);

        RuleForEach(request => request.Sets)
            .SetValidator(new SessionSetRequestValidator());
    }
}
