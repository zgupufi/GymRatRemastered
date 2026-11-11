using FluentValidation;
using GymRatService.Common.DTOs.Sessions;

namespace GymRatService.API.Validators;

public class SessionSetRequestValidator : AbstractValidator<SessionSetRequest>
{
    private const int MinimumReps = 1;

    private const int MaximumReps = 1000;

    private const decimal MinimumWeightKg = 0;

    private const decimal MaximumWeightKg = 2000;

    private const int MinimumRepsInReserve = 0;

    private const int MaximumRepsInReserve = 3;

    public SessionSetRequestValidator()
    {
        RuleFor(request => request.Reps)
            .InclusiveBetween(MinimumReps, MaximumReps);

        RuleFor(request => request.WeightKg)
            .InclusiveBetween(MinimumWeightKg, MaximumWeightKg)
            .PrecisionScale(7, 2, false);

        RuleFor(request => request.RepsInReserve)
            .InclusiveBetween(MinimumRepsInReserve, MaximumRepsInReserve)
            .When(request => request.RepsInReserve is not null);
    }
}
