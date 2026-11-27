namespace GymRatService.Service.Usage;

public sealed record ChatUsageReservation(Guid UserId, DateOnly Day, long Tokens);
