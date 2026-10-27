using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository;

public class DatabaseInitializer(GymRatDbContext context, IExerciseCatalogSeeder exerciseCatalogSeeder) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await context.Database.MigrateAsync(cancellationToken);

        await exerciseCatalogSeeder.SeedAsync(cancellationToken);
    }
}
