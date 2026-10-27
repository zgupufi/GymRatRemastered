using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Repository;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Repository.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymRatService.IntegrationTests.Database;

public sealed class ExerciseCatalogSeederTests : IDisposable
{
    private const int CatalogSize = 89;

    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Catalog_HasUniqueGuidV7IdsAndUniqueNames()
    {
        // Act
        var catalog = await ExerciseCatalogSeeder.LoadCatalogAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CatalogSize, catalog.Count);
        Assert.All(catalog, exercise => Assert.Equal(7, exercise.Id.Version));
        Assert.Equal(CatalogSize, catalog.Select(exercise => exercise.Id).Distinct().Count());
        Assert.Equal(CatalogSize, catalog.Select(exercise => exercise.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(catalog, exercise =>
        {
            Assert.False(string.IsNullOrWhiteSpace(exercise.Name));
            Assert.False(string.IsNullOrWhiteSpace(exercise.MainMuscle));
            Assert.False(string.IsNullOrWhiteSpace(exercise.SubMuscles));
            Assert.False(string.IsNullOrWhiteSpace(exercise.Equipment));
            Assert.False(string.IsNullOrWhiteSpace(exercise.Difficulty));
            Assert.False(string.IsNullOrWhiteSpace(exercise.Description));
            Assert.StartsWith("https://www.youtube.com/", exercise.YoutubeLink);
        });
    }

    [Fact]
    public async Task SeedAsync_InsertsTheWholeCatalog_WhenTheDatabaseIsEmpty()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });

        // Act
        var inserted = await SeedCatalogAsync();

        // Assert
        Assert.Equal(CatalogSize, inserted);
        var exercises = await GetStoredExercisesAsync();
        Assert.Equal(CatalogSize, exercises.Count);
        var benchPress = Assert.Single(exercises, exercise => exercise.Name == "Bench Press");
        Assert.Equal("Chest", benchPress.MainMuscle);
        Assert.Equal("Shoulders, Triceps", benchPress.SubMuscles);
        Assert.Equal("Barbell", benchPress.Equipment);
        Assert.Equal("Intermediate", benchPress.Difficulty);
        Assert.Equal("https://www.youtube.com/watch?v=vcBig73ojpE", benchPress.YoutubeLink);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRunMoreThanOnce()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        await SeedCatalogAsync();
        var idsAfterFirstRun = (await GetStoredExercisesAsync()).Select(exercise => exercise.Id).Order().ToList();

        // Act
        var inserted = await SeedCatalogAsync();

        // Assert
        Assert.Equal(0, inserted);
        var idsAfterSecondRun = (await GetStoredExercisesAsync()).Select(exercise => exercise.Id).Order().ToList();
        Assert.Equal(idsAfterFirstRun, idsAfterSecondRun);
    }

    [Fact]
    public async Task SeedAsync_KeepsExistingExercises_WhenOneWithTheSameNameAlreadyExists()
    {
        // Arrange
        var customBenchPress = new ExerciseCard { Name = "bench press", MainMuscle = "Custom" };
        await _factory.SeedAsync(context => context.Exercises.Add(customBenchPress));

        // Act
        var inserted = await SeedCatalogAsync();

        // Assert
        Assert.Equal(CatalogSize - 1, inserted);
        var exercises = await GetStoredExercisesAsync();
        Assert.Equal(CatalogSize, exercises.Count);
        var benchPress = Assert.Single(exercises, exercise => exercise.Name.Equals("Bench Press", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(customBenchPress.Id, benchPress.Id);
        Assert.Equal("Custom", benchPress.MainMuscle);
    }

    [Fact]
    public async Task GetExercises_ReturnsTheSeededCatalog()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        await SeedCatalogAsync();
        var catalog = await ExerciseCatalogSeeder.LoadCatalogAsync(CancellationToken.None);
        var deadlift = Assert.Single(catalog, exercise => exercise.Name == "Deadlift");
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exercises = await response.Content.ReadFromJsonAsync<List<ExerciseDto>>();
        Assert.NotNull(exercises);
        Assert.Equal(CatalogSize, exercises.Count);
        Assert.Contains(
            new ExerciseDto(
                deadlift.Id,
                "Deadlift",
                "Back",
                "Hamstrings, Glutes, Forearms",
                "Barbell",
                "Advanced",
                deadlift.Description,
                "https://www.youtube.com/watch?v=ytGaGIn3SjE"),
            exercises);
    }

    [Fact]
    public void Migrations_MatchTheCurrentModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GymRatDbContext>()
            .UseNpgsql("Host=localhost;Database=gymrat_model_check")
            .Options;
        using var context = new GymRatDbContext(options);

        // Act
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Assert
        Assert.False(hasPendingChanges);
        Assert.Contains("InitialCreate", context.Database.GetMigrations().Single());
    }

    private async Task<int> SeedCatalogAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IExerciseCatalogSeeder>();

        return await seeder.SeedAsync(CancellationToken.None);
    }

    private async Task<List<ExerciseCard>> GetStoredExercisesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GymRatDbContext>();

        return await context.Exercises.AsNoTracking().ToListAsync();
    }
}
