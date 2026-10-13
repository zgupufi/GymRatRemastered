namespace GymRatService.Service.Exceptions;

/// <summary>
/// A business rule failure. The API turns it into a 400 response carrying <see cref="Code"/>.
/// </summary>
public class ServiceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
