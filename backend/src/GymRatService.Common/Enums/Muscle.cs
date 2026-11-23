using System.Text.Json.Serialization;

namespace GymRatService.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Muscle
{
    Back,
    Biceps,
    Brachialis,
    Calves,
    Chest,
    Core,
    Forearms,
    FrontDeltoids,
    Glutes,
    Hamstrings,
    HipAbductors,
    HipFlexors,
    InnerThighs,
    Lats,
    LongHeadOfBiceps,
    LongHeadOfTriceps,
    LowerAbs,
    LowerBack,
    LowerChest,
    LowerLats,
    MedialDeltoids,
    MiddleTrapezius,
    Obliques,
    Quadriceps,
    RearDeltoids,
    RectusAbdominis,
    Rhomboids,
    RotatorCuff,
    Shoulders,
    TeresMajor,
    Traps,
    Triceps,
    UpperChest,
    UpperTrapezius,
}
