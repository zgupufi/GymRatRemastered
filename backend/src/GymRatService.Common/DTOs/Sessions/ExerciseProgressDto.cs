namespace GymRatService.Common.DTOs.Sessions;

public sealed record ExerciseProgressDto(DateOnly Date, BestSetDto BestSet, decimal EstimatedOneRepMax);
