using GymRatService.Common.Enums;

namespace GymRatService.Repository.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; }

    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kilograms;

    public int DefaultRestTimeSeconds { get; set; } = 90;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
