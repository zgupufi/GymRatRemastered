using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Users;

public sealed record UserProfileDto(
    string FirstName,
    string? LastName,
    string Email,
    WeightUnit WeightUnit,
    int DefaultRestTimeSeconds);
