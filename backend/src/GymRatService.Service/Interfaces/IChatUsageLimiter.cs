namespace GymRatService.Service.Interfaces;

public interface IChatUsageLimiter
{
    void EnsureAllowed(Guid userId);

    void RecordTokens(Guid userId, long tokens);
}
