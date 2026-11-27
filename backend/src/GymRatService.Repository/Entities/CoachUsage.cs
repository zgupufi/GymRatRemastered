namespace GymRatService.Repository.Entities;

public class CoachUsage
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Day { get; set; }

    public int RequestCount { get; set; }

    public long TokensUsed { get; set; }

    public string RecentRequests { get; set; } = string.Empty;

    public int Version { get; set; }
}
