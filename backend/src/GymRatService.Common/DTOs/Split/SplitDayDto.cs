using GymRatService.Common.DTOs.Workouts;

namespace GymRatService.Common.DTOs.Split;

public sealed record SplitDayDto(int Day, WorkoutSummaryDto? Workout);
