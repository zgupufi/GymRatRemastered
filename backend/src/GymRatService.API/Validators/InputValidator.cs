using FluentValidation;
using GymRatService.Service.Interfaces;

namespace GymRatService.API.Validators;

public class InputValidator(IServiceProvider serviceProvider) : IInputValidator
{
    public async Task ValidateAsync(object input, CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(input.GetType());

        if (serviceProvider.GetService(validatorType) is not IValidator validator)
        {
            return;
        }

        var result = await validator.ValidateAsync(new ValidationContext<object>(input), cancellationToken);

        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
}
