namespace GymRatService.Repository.Entities;

public class Workout
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<WorkoutExercise> Exercises { get; set; } = [];
}
