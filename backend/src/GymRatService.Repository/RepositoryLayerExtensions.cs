using GymRatService.Repository.Interfaces;
using GymRatService.Repository.Repositories;
using GymRatService.Repository.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.Repository;

public static class RepositoryLayerExtensions
{
    public static IServiceCollection AddRepositoryLayer(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<GymRatDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IExerciseCardsRepository, ExerciseCardsRepository>();
        services.AddScoped<IExerciseCatalogSeeder, ExerciseCatalogSeeder>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var databaseInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();

        await databaseInitializer.InitializeAsync(cancellationToken);
    }
}
