namespace GymRatService.Repository.Entities;

public class ExerciseCard
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string MainMuscle { get; set; } = string.Empty;

    public string? SubMuscles { get; set; }

    public string? Equipment { get; set; }

    public string? Difficulty { get; set; }

    public string? Description { get; set; }

    public string? YoutubeLink { get; set; }
}
