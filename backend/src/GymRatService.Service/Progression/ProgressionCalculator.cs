using GymRatService.Common.Enums;

namespace GymRatService.Service.Progression;

public static class ProgressionCalculator
{
    private const int MaximumRepsInReserveForHold = 0;

    public static ProgressionTarget Calculate(ProgressionSettings settings, IReadOnlyList<LoggedSet> last, IReadOnlyList<LoggedSet> previous)
    {
        if (last.Count == 0)
        {
            return new ProgressionTarget(null, settings.RepsMin, ProgressionReason.NoHistory, null, []);
        }

        var working = WorkingSets(last);
        var weight = working[0].WeightKg;
        var lowestReps = working.Min(set => set.Reps);
        var lastReps = working.Select(set => set.Reps).ToList();

        if (weight == 0)
        {
            return IsAtMaximumEffort(working)
                ? new ProgressionTarget(0, lowestReps, HoldReason(settings, previous, weight), weight, lastReps)
                : new ProgressionTarget(0, lowestReps + 1, ProgressionReason.AddRep, weight, lastReps);
        }

        if (IsHold(settings, working))
        {
            var reps = Math.Clamp(lowestReps, settings.RepsMin, settings.RepsMax);

            return new ProgressionTarget(weight, reps, HoldReason(settings, previous, weight), weight, lastReps);
        }

        if (working.All(set => set.Reps >= settings.RepsMax))
        {
            return new ProgressionTarget(IncreaseWeight(weight, settings.WeightStepKg), settings.RepsMin, ProgressionReason.IncreaseWeight, weight, lastReps);
        }

        return new ProgressionTarget(weight, Math.Min(lowestReps + 1, settings.RepsMax), ProgressionReason.AddRep, weight, lastReps);
    }

    public static decimal IncreaseWeight(decimal weightKg, decimal stepKg)
    {
        var rounded = Math.Round((weightKg + stepKg) / stepKg, MidpointRounding.AwayFromZero) * stepKg;

        return rounded > weightKg ? rounded : rounded + stepKg;
    }

    private static List<LoggedSet> WorkingSets(IReadOnlyList<LoggedSet> sets)
    {
        return sets
            .GroupBy(set => set.WeightKg)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .First()
            .ToList();
    }

    private static bool IsAtMaximumEffort(IEnumerable<LoggedSet> working) =>
        working.Any(set => set.RepsInReserve <= MaximumRepsInReserveForHold);

    private static bool IsHold(ProgressionSettings settings, IReadOnlyList<LoggedSet> working) =>
        IsAtMaximumEffort(working) || working.Any(set => set.Reps < settings.RepsMin);

    private static ProgressionReason HoldReason(ProgressionSettings settings, IReadOnlyList<LoggedSet> previous, decimal weight)
    {
        if (previous.Count == 0)
        {
            return ProgressionReason.Hold;
        }

        var working = WorkingSets(previous);

        if (working[0].WeightKg != weight)
        {
            return ProgressionReason.Hold;
        }

        var wasHold = weight == 0 ? IsAtMaximumEffort(working) : IsHold(settings, working);

        return wasHold ? ProgressionReason.Stalled : ProgressionReason.Hold;
    }
}
