namespace GymRatService.Repository.Entities;

public class SplitDay
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int Day { get; set; }

    public Guid WorkoutId { get; set; }

    public Workout? Workout { get; set; }
}
