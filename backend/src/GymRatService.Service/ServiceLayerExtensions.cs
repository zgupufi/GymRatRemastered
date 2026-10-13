using GymRatService.Repository;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.Service;

public static class ServiceLayerExtensions
{
    /// <summary>Registers the services and, beneath them, the repository layer.</summary>
    public static IServiceCollection AddServiceLayer(this IServiceCollection services, string? connectionString)
    {
        services.AddRepositoryLayer(connectionString);

        services.AddScoped<IExercisesService, ExercisesService>();

        return services;
    }
}
