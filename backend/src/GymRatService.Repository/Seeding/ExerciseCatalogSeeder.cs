using System.Text.Json;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymRatService.Repository.Seeding;

public class ExerciseCatalogSeeder(GymRatDbContext context) : IExerciseCatalogSeeder
{
    private const string CatalogResourceName = "GymRatService.Repository.Seeding.ExerciseCatalog.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var catalog = await LoadCatalogAsync(cancellationToken);

        var existing = await context.Exercises
            .AsNoTracking()
            .Select(exercise => new { exercise.Id, exercise.Name })
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(exercise => exercise.Id).ToHashSet();
        var existingNames = existing.Select(exercise => exercise.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = catalog
            .Where(exercise => !existingIds.Contains(exercise.Id) && !existingNames.Contains(exercise.Name))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        context.Exercises.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    public static async Task<IReadOnlyList<ExerciseCard>> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(ExerciseCatalogSeeder).Assembly.GetManifestResourceStream(CatalogResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {CatalogResourceName} was not found.");

        return await JsonSerializer.DeserializeAsync<List<ExerciseCard>>(stream, SerializerOptions, cancellationToken)
            ?? [];
    }
}
