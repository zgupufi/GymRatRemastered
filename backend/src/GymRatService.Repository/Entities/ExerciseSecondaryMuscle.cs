using GymRatService.Common.Enums;

namespace GymRatService.Repository.Entities;

public class ExerciseSecondaryMuscle
{
    public Guid ExerciseId { get; set; }

    public Muscle Muscle { get; set; }
}
