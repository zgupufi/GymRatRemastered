namespace GymRatService.Service.Interfaces;

public interface IInputValidator
{
    Task ValidateAsync(object input, CancellationToken cancellationToken);
}
