using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Users;

public sealed record UpdateUserSettingsRequest(
    WeightUnit? WeightUnit,
    int? DefaultRestTimeSeconds);
