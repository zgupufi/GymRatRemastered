using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Exercises;

public sealed class ExercisesEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetExercises_ReturnsEveryExerciseOrderedByName()
    {
        // Arrange
        await _factory.SeedAsync(context => context.Exercises.AddRange(
            new ExerciseCard { Name = "Squat", MainMuscle = "Legs", Equipment = "Barbell" },
            new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Difficulty = "Intermediate" }));
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/exercises");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exercises = await response.Content.ReadFromJsonAsync<List<ExerciseDto>>();
        Assert.NotNull(exercises);
        Assert.Collection(
            exercises,
            first =>
            {
                Assert.Equal("Bench Press", first.Name);
                Assert.Equal("Chest", first.MainMuscle);
                Assert.Equal("Intermediate", first.Difficulty);
            },
            second =>
            {
                Assert.Equal("Squat", second.Name);
                Assert.Equal("Legs", second.MainMuscle);
                Assert.Equal("Barbell", second.Equipment);
            });
    }

    [Fact]
    public async Task GetExercise_ReturnsTheExercise_WhenItExists()
    {
        // Arrange
        var deadlift = new ExerciseCard { Name = "Deadlift", MainMuscle = "Back", SubMuscles = "Hamstrings" };
        await _factory.SeedAsync(context => context.Exercises.Add(deadlift));
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/exercises/{deadlift.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exercise = await response.Content.ReadFromJsonAsync<ExerciseDto>();
        Assert.Equal(
            new ExerciseDto(deadlift.Id, "Deadlift", "Back", "Hamstrings", null, null, null, null),
            exercise);
    }

    [Fact]
    public async Task GetExercise_ReturnsBadRequestNamingTheError_WhenItDoesNotExist()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/exercises/999");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.ExerciseNotFound, error?.Code);
    }
}
