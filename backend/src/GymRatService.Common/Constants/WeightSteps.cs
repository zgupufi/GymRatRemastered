using GymRatService.Common.Enums;

namespace GymRatService.Common.Constants;

public static class WeightSteps
{
    public const decimal Barbell = 2.5m;

    public const decimal Dumbbell = 2m;

    public const decimal Other = 1.25m;

    public static IReadOnlyList<decimal> Allowed { get; } = [1m, 1.25m, 2m, 2.5m, 5m];

    public static decimal ForEquipment(Equipment equipment) => equipment switch
    {
        Equipment.Barbell => Barbell,
        Equipment.Dumbbells => Dumbbell,
        _ => Other,
    };
}
