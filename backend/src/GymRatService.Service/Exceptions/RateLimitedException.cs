namespace GymRatService.Service.Exceptions;

public class RateLimitedException(string code, TimeSpan retryAfter) : BusinessLogicException(code)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
