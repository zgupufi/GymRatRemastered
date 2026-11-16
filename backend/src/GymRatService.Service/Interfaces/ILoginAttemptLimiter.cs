namespace GymRatService.Service.Interfaces;

public interface ILoginAttemptLimiter
{
    void EnsureAllowed(string email);

    void RecordFailure(string email);

    void Reset(string email);
}
