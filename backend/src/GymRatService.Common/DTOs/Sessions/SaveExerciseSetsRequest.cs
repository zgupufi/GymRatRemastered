namespace GymRatService.Common.DTOs.Sessions;

public sealed record SaveExerciseSetsRequest(IReadOnlyList<SessionSetRequest> Sets);
