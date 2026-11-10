using GymRatService.Common.Enums;

namespace GymRatService.Repository.Entities;

public class CoachSuggestion
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid WorkoutId { get; set; }

    public Guid ExerciseId { get; set; }

    public int? TargetSets { get; set; }

    public int? RepsMin { get; set; }

    public int? RepsMax { get; set; }

    public decimal? WeightStepKg { get; set; }

    public int BeforeTargetSets { get; set; }

    public int BeforeRepsMin { get; set; }

    public int BeforeRepsMax { get; set; }

    public decimal BeforeWeightStepKg { get; set; }

    public string Rationale { get; set; } = string.Empty;

    public SuggestionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Workout? Workout { get; set; }

    public ExerciseCard? Exercise { get; set; }
}
