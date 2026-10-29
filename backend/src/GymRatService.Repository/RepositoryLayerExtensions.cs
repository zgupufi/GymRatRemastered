using GymRatService.Repository.Interfaces;
using GymRatService.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.Repository;

public static class RepositoryLayerExtensions
{
    public static IServiceCollection AddRepositoryLayer(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<GymRatDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IExerciseCardsRepository, ExerciseCardsRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IWorkoutsRepository, WorkoutsRepository>();
        services.AddScoped<ISplitRepository, SplitRepository>();

        return services;
    }
}
