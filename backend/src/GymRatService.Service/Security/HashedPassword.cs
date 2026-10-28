namespace GymRatService.Service.Security;

public sealed record HashedPassword(string Hash, string Salt);
