using GymRatService.Repository;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.Service;

public static class ServiceLayerExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services, string? connectionString)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.AddRepositoryLayer(connectionString);

        services.AddScoped<IExercisesService, ExercisesService>();

        return services;
    }
}
