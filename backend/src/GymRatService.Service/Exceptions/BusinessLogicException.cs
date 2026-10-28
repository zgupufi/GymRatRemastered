namespace GymRatService.Service.Exceptions;

public class BusinessLogicException(string code, params object[] arguments) : Exception(code)
{
    public string Code { get; } = code;

    public IReadOnlyList<object> Arguments { get; } = arguments;
}
