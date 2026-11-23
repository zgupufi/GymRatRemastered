using GymRatService.Common.Enums;

namespace GymRatService.Repository.Entities;

public class ExerciseCard
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public MuscleGroup MainMuscle { get; set; }

    public Equipment Equipment { get; set; }

    public Difficulty Difficulty { get; set; }

    public MovementPattern MovementPattern { get; set; }

    public string? YoutubeLink { get; set; }

    public List<ExerciseSecondaryMuscle> SecondaryMuscles { get; set; } = [];

    public List<ExerciseTranslation> Translations { get; set; } = [];
}
