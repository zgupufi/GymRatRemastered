namespace GymRatService.Repository.Interfaces;

public interface IExerciseCatalogSeeder
{
    Task<int> SeedAsync(CancellationToken cancellationToken);
}
