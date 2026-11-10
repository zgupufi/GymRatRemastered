using GymRatService.Common.Enums;
using GymRatService.Service.Progression;

namespace GymRatService.BLL.Tests.Progression;

public class ProgressionCalculatorTests
{
    private static readonly ProgressionSettings Range = new(8, 12, 2.5m);

    [Fact]
    public void Calculate_ReturnsNoHistoryWithTheBottomOfTheRange_WhenThereAreNoSets()
    {
        // Act
        var target = ProgressionCalculator.Calculate(Range, [], []);

        // Assert
        Assert.Equal((null, 8, ProgressionReason.NoHistory, null), (target.WeightKg, target.Reps, target.Reason, target.LastWeightKg));
        Assert.Empty(target.LastReps);
    }

    [Fact]
    public void Calculate_IncreasesTheWeightAndResetsReps_WhenEveryWorkingSetReachedTheTop()
    {
        // Act
        var target = ProgressionCalculator.Calculate(Range, [Set(12, 60), Set(12, 60), Set(13, 60)], []);

        // Assert
        Assert.Equal((62.5m, 8, ProgressionReason.IncreaseWeight), (target.WeightKg, target.Reps, target.Reason));
        Assert.Equal(60m, target.LastWeightKg);
        Assert.Equal([12, 12, 13], target.LastReps);
    }

    [Fact]
    public void Calculate_AddsARepCappedAtTheTop_WhenTheSetsAreInsideTheRange()
    {
        // Act
        var added = ProgressionCalculator.Calculate(Range, [Set(9, 60), Set(8, 60)], []);
        var capped = ProgressionCalculator.Calculate(Range, [Set(12, 60), Set(11, 60)], []);

        // Assert
        Assert.Equal((60m, 9, ProgressionReason.AddRep), (added.WeightKg, added.Reps, added.Reason));
        Assert.Equal((60m, 12, ProgressionReason.AddRep), (capped.WeightKg, capped.Reps, capped.Reason));
    }

    [Fact]
    public void Calculate_Holds_WhenAWorkingSetWasAtMaximumEffort()
    {
        // Act
        var target = ProgressionCalculator.Calculate(Range, [Set(12, 60, 1), Set(12, 60, 0)], []);

        // Assert
        Assert.Equal((60m, 12, ProgressionReason.Hold), (target.WeightKg, target.Reps, target.Reason));
    }

    [Fact]
    public void Calculate_HoldsWithRepsClampedToTheRange_WhenAWorkingSetMissedTheBottom()
    {
        // Act
        var target = ProgressionCalculator.Calculate(Range, [Set(8, 60, 3), Set(6, 60, 3)], []);

        // Assert
        Assert.Equal((60m, 8, ProgressionReason.Hold), (target.WeightKg, target.Reps, target.Reason));
    }

    [Fact]
    public void Calculate_ReportsStalled_WhenThePreviousSessionWasAlsoAHoldAtTheSameWeight()
    {
        // Act
        var stalled = ProgressionCalculator.Calculate(Range, [Set(8, 80, 0)], [Set(8, 80, 0)]);
        var otherWeight = ProgressionCalculator.Calculate(Range, [Set(8, 80, 0)], [Set(8, 77.5m, 0)]);
        var previousProgressed = ProgressionCalculator.Calculate(Range, [Set(8, 80, 0)], [Set(10, 80, 2)]);

        // Assert
        Assert.Equal((80m, 8, ProgressionReason.Stalled), (stalled.WeightKg, stalled.Reps, stalled.Reason));
        Assert.Equal(ProgressionReason.Hold, otherWeight.Reason);
        Assert.Equal(ProgressionReason.Hold, previousProgressed.Reason);
    }

    [Fact]
    public void Calculate_UsesTheWeightWithTheMostSetsAndTheHeavierOneOnTies()
    {
        // Act
        var typo = ProgressionCalculator.Calculate(Range, [Set(10, 60), Set(10, 600), Set(10, 60)], []);
        var tie = ProgressionCalculator.Calculate(Range, [Set(10, 60), Set(9, 65)], []);

        // Assert
        Assert.Equal(60m, typo.WeightKg);
        Assert.Equal([10, 10], typo.LastReps);
        Assert.Equal((65m, 10), (tie.WeightKg, tie.Reps));
    }

    [Fact]
    public void Calculate_ProgressesRepsWithoutACap_ForBodyweightExercises()
    {
        // Act
        var added = ProgressionCalculator.Calculate(Range, [Set(15, 0), Set(14, 0)], []);
        var held = ProgressionCalculator.Calculate(Range, [Set(5, 0, 0)], []);
        var stalled = ProgressionCalculator.Calculate(Range, [Set(5, 0, 0)], [Set(5, 0, 0)]);
        var previousEasy = ProgressionCalculator.Calculate(Range, [Set(5, 0, 0)], [Set(5, 0, 2)]);

        // Assert
        Assert.Equal((0m, 15, ProgressionReason.AddRep), (added.WeightKg, added.Reps, added.Reason));
        Assert.Equal((0m, 5, ProgressionReason.Hold), (held.WeightKg, held.Reps, held.Reason));
        Assert.Equal(ProgressionReason.Stalled, stalled.Reason);
        Assert.Equal(ProgressionReason.Hold, previousEasy.Reason);
    }

    [Theory]
    [InlineData(60, 2.5, 62.5)]
    [InlineData(31, 2, 34)]
    [InlineData(32, 2, 34)]
    [InlineData(20.1, 1.25, 21.25)]
    [InlineData(100, 5, 105)]
    public void IncreaseWeight_RoundsToTheStepAndAlwaysGoesUp(double weightKg, double stepKg, double expected)
    {
        // Act
        var result = ProgressionCalculator.IncreaseWeight((decimal)weightKg, (decimal)stepKg);

        // Assert
        Assert.Equal((decimal)expected, result);
    }

    public static TheoryData<int, int, decimal> Grid()
    {
        var data = new TheoryData<int, int, decimal>();
        foreach (var repsMin in new[] { 1, 5, 8 })
        {
            foreach (var repsMax in new[] { 8, 12, 30 })
            {
                foreach (var step in new[] { 1m, 1.25m, 2m, 2.5m, 5m })
                {
                    data.Add(repsMin, repsMax, step);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void Calculate_KeepsItsInvariantsForEveryCombination(int repsMin, int repsMax, decimal step)
    {
        // Arrange
        var settings = new ProgressionSettings(repsMin, repsMax, step);
        int?[] efforts = [null, 0, 1, 3];

        foreach (var weight in new[] { 0m, 20m, 61.3m })
        {
            foreach (var reps in new[] { 1, 6, 8, 12, 20, 31 })
            {
                foreach (var effort in efforts)
                {
                    // Act
                    var target = ProgressionCalculator.Calculate(settings, [Set(reps, weight, effort), Set(reps, weight)], []);

                    // Assert
                    Assert.NotNull(target.WeightKg);
                    Assert.True(target.WeightKg >= weight);
                    Assert.Equal(target.Reason == ProgressionReason.IncreaseWeight, target.WeightKg > weight);
                    Assert.True(target.Reps >= 1);
                    if (weight > 0)
                    {
                        Assert.InRange(target.Reps, repsMin, repsMax);
                    }
                }
            }
        }
    }

    private static LoggedSet Set(int reps, decimal weightKg, int? repsInReserve = null) => new(reps, weightKg, repsInReserve);
}
