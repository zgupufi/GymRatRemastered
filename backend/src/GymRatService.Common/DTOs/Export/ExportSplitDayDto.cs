namespace GymRatService.Common.DTOs.Export;

public sealed record ExportSplitDayDto(int Day, Guid WorkoutId, string WorkoutName);
