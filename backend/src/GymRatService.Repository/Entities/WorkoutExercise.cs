namespace GymRatService.Repository.Entities;

public class WorkoutExercise
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }

    public Guid ExerciseId { get; set; }

    public int Position { get; set; }

    public int TargetSets { get; set; }

    public ExerciseCard? Exercise { get; set; }
}
