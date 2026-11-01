using FluentValidation;
using GymRatService.Common.DTOs.Sessions;

namespace GymRatService.Service.Validators;

public class SessionSetRequestValidator : AbstractValidator<SessionSetRequest>
{
    private const int MinimumReps = 1;

    private const int MaximumReps = 1000;

    private const decimal MinimumWeightKg = 0;

    private const decimal MaximumWeightKg = 2000;

    public SessionSetRequestValidator()
    {
        RuleFor(request => request.Reps)
            .InclusiveBetween(MinimumReps, MaximumReps);

        RuleFor(request => request.WeightKg)
            .InclusiveBetween(MinimumWeightKg, MaximumWeightKg)
            .PrecisionScale(7, 2, false);
    }
}
