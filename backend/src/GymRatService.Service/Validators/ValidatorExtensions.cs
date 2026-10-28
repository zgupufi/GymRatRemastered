using FluentValidation;
using GymRatService.Service.Exceptions;

namespace GymRatService.Service.Validators;

public static class ValidatorExtensions
{
    public static async Task ValidateOrThrowAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (!result.IsValid)
        {
            var error = result.Errors[0];

            throw new ServiceException(error.ErrorCode, error.ErrorMessage);
        }
    }
}
