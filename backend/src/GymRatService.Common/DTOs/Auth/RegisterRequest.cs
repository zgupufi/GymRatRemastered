using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string? LastName,
    WeightUnit WeightUnit);
