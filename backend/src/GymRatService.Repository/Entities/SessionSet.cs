using GymRatService.Common.Enums;

namespace GymRatService.Repository.Entities;

public class SessionSet
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid ExerciseId { get; set; }

    public int SetNumber { get; set; }

    public int Reps { get; set; }

    public decimal WeightKg { get; set; }

    public int? RepsInReserve { get; set; }

    public decimal? TargetWeightKg { get; set; }

    public int? TargetReps { get; set; }

    public ProgressionReason? TargetReason { get; set; }

    public WorkoutSession? Session { get; set; }
}
