namespace GymRatService.Repository.Entities;

public class WorkoutSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? WorkoutId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public List<SessionSet> Sets { get; set; } = [];
}
