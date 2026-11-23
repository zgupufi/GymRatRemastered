namespace GymRatService.Repository.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset SessionStartedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RotatedAt { get; set; }

    public int RotationCount { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
