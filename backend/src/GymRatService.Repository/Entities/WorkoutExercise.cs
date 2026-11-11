namespace GymRatService.Repository.Entities;

public class WorkoutExercise
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }

    public Guid ExerciseId { get; set; }

    public int Position { get; set; }

    public int TargetSets { get; set; }

    public int RepsMin { get; set; } = 8;

    public int RepsMax { get; set; } = 12;

    public decimal WeightStepKg { get; set; } = 2.5m;

    public ExerciseCard? Exercise { get; set; }
}
