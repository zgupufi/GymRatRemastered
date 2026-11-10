using GymRatService.Common.Enums;

namespace GymRatService.Service.Progression;

public sealed record ProgressionTarget(decimal? WeightKg, int Reps, ProgressionReason Reason, decimal? LastWeightKg, IReadOnlyList<int> LastReps);
