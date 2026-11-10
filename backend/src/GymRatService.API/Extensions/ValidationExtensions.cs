using FluentValidation;
using GymRatService.API.Filters;
using GymRatService.API.Validators;
using GymRatService.Service.Interfaces;

namespace GymRatService.API.Extensions;

public static class ValidationExtensions
{
    public static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        var validators = typeof(ValidationExtensions).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(contract => (Contract: contract, Implementation: type)));

        foreach (var (contract, implementation) in validators)
        {
            services.AddSingleton(contract, implementation);
        }

        services.AddSingleton<IInputValidator, InputValidator>();
        services.AddScoped<ValidationActionFilter>();

        return services;
    }
}
