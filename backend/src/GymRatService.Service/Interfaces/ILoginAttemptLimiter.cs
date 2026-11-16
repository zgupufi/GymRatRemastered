namespace GymRatService.Service.Interfaces;

public interface ILoginAttemptLimiter
{
    void RegisterAttempt(string email);

    void Reset(string email);
}
