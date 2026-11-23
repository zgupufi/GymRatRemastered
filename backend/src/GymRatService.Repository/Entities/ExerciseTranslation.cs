namespace GymRatService.Repository.Entities;

public class ExerciseTranslation
{
    public Guid ExerciseId { get; set; }

    public string Culture { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Setup { get; set; } = string.Empty;

    public string Execution { get; set; } = string.Empty;
}
