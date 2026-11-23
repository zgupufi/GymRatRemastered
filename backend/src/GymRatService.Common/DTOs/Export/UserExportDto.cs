using GymRatService.Common.DTOs.Users;

namespace GymRatService.Common.DTOs.Export;

public sealed record UserExportDto(
    DateTimeOffset ExportedAt,
    UserProfileDto Profile,
    IReadOnlyList<ExportWorkoutDto> Workouts,
    IReadOnlyList<ExportSplitDayDto> Split,
    IReadOnlyList<ExportSessionDto> Sessions);
