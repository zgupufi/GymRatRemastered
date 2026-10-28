using GymRatService.Repository;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using GymRatService.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymRatService.Service;

public static class ServiceLayerExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services, string? connectionString)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.AddRepositoryLayer(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IExercisesService, ExercisesService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsersService, UsersService>();

        return services;
    }
}
