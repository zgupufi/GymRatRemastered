using GymRatService.Common.Enums;

namespace GymRatService.Common.DTOs.Sessions;

public sealed record ServedTargetRequest(decimal? WeightKg, int Reps, ProgressionReason Reason);
