namespace GymRatService.Common.DTOs.Sessions;

public sealed record LastSessionExerciseDto(Guid ExerciseId, IReadOnlyList<SessionSetDto> Sets);
