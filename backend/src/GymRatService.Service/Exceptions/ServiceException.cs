namespace GymRatService.Service.Exceptions;

public class ServiceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
