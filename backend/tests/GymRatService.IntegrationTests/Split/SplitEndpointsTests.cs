using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Split;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Split;

public sealed class SplitEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("GET", "/api/split")]
    [InlineData("PUT", "/api/split/1")]
    public async Task Split_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated(string method, string url)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSplit_ReturnsSevenRestDays_WhenNothingIsAssigned()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var days = await client.GetFromJsonAsync<List<SplitDayDto>>("/api/split");

        // Assert
        Assert.NotNull(days);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], days.Select(day => day.Day));
        Assert.All(days, day => Assert.Null(day.Workout));
    }

    [Fact]
    public async Task AssignSplitDay_ReturnsNoContentAndTheDayShowsTheWorkoutWithItsMainMuscles()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var push = await CreateWorkoutAsync(client, "Push", (bench.Id, 3), (squat.Id, 4));

        // Act
        var response = await client.PutAsJsonAsync("/api/split/1", new AssignSplitDayRequest(push));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var days = await client.GetFromJsonAsync<List<SplitDayDto>>("/api/split");
        Assert.NotNull(days);
        var monday = days[0].Workout;
        Assert.Equal(push, monday?.Id);
        Assert.Equal("Push", monday?.Name);
        Assert.Equal(2, monday?.ExerciseCount);
        Assert.Equal(["Chest", "Legs"], monday?.MainMuscles);
        Assert.All(days.Skip(1), day => Assert.Null(day.Workout));
    }

    [Fact]
    public async Task AssignSplitDay_ReplacesTheWorkout_WhenTheDayIsAlreadyAssigned()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var push = await CreateWorkoutAsync(client, "Push");
        var pull = await CreateWorkoutAsync(client, "Pull");
        await client.PutAsJsonAsync("/api/split/3", new AssignSplitDayRequest(push));

        // Act
        var response = await client.PutAsJsonAsync("/api/split/3", new AssignSplitDayRequest(pull));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var days = await client.GetFromJsonAsync<List<SplitDayDto>>("/api/split");
        Assert.Equal(pull, days![2].Workout?.Id);
        Assert.Equal(1, days.Count(day => day.Workout is not null));
    }

    [Fact]
    public async Task AssignSplitDay_ClearsTheDay_WhenNoWorkoutIsGiven()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var push = await CreateWorkoutAsync(client, "Push");
        await client.PutAsJsonAsync("/api/split/2", new AssignSplitDayRequest(push));

        // Act
        var response = await client.PutAsJsonAsync("/api/split/2", new AssignSplitDayRequest(null));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var days = await client.GetFromJsonAsync<List<SplitDayDto>>("/api/split");
        Assert.All(days!, day => Assert.Null(day.Workout));
    }

    [Fact]
    public async Task AssignSplitDay_ReturnsBadRequestWithWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var push = await CreateWorkoutAsync(owner, "Push");

        // Act
        var response = await intruder.PutAsJsonAsync("/api/split/1", new AssignSplitDayRequest(push));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.WorkoutNotFound, error?.Code);
        var days = await intruder.GetFromJsonAsync<List<SplitDayDto>>("/api/split");
        Assert.All(days!, day => Assert.Null(day.Workout));
    }

    [Fact]
    public async Task AssignSplitDay_ReturnsBadRequestWithWorkoutNotFound_WhenTheWorkoutDoesNotExist()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PutAsJsonAsync("/api/split/1", new AssignSplitDayRequest(Guid.CreateVersion7()));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.WorkoutNotFound, error?.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task AssignSplitDay_ReturnsBadRequestWithValidationErrors_WhenTheDayIsOutsideTheWeek(int day)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PutAsJsonAsync($"/api/split/{day}", new AssignSplitDayRequest(null));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Single(error!.Errors!);
    }

    [Fact]
    public async Task GetSplit_OnlyReturnsTheCallersOwnAssignments()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var ana = await CreateClientAsync("ana@example.com");
        var bob = await CreateClientAsync("bob@example.com");
        var push = await CreateWorkoutAsync(ana, "Push");
        await ana.PutAsJsonAsync("/api/split/1", new AssignSplitDayRequest(push));

        // Act
        var days = await bob.GetFromJsonAsync<List<SplitDayDto>>("/api/split");

        // Assert
        Assert.All(days!, day => Assert.Null(day.Workout));
    }

    [Fact]
    public async Task DeleteWorkout_ClearsTheDaysItWasAssignedTo()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var push = await CreateWorkoutAsync(client, "Push");
        var pull = await CreateWorkoutAsync(client, "Pull");
        await client.PutAsJsonAsync("/api/split/1", new AssignSplitDayRequest(push));
        await client.PutAsJsonAsync("/api/split/4", new AssignSplitDayRequest(push));
        await client.PutAsJsonAsync("/api/split/6", new AssignSplitDayRequest(pull));

        // Act
        var response = await client.DeleteAsync($"/api/workouts/{push}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var days = await client.GetFromJsonAsync<List<SplitDayDto>>("/api/split");
        Assert.Equal([6], days!.Where(day => day.Workout is not null).Select(day => day.Day));
        Assert.Equal(pull, days![5].Workout?.Id);
    }

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }

    private async Task<(ExerciseCard Bench, ExerciseCard Squat)> SeedExercisesAsync()
    {
        var bench = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Difficulty = "Intermediate" };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = "Legs", SubMuscles = "Glutes", Equipment = "Barbell" };
        await _factory.SeedAsync(context => context.Exercises.AddRange(bench, squat));

        return (bench, squat);
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, string name, params (Guid ExerciseId, int TargetSets)[] entries)
    {
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest(name));
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        var id = created?.Id ?? throw new InvalidOperationException("Create workout did not return an id.");

        if (entries.Length > 0)
        {
            var request = new ReplaceWorkoutExercisesRequest(
                [.. entries.Select(entry => new WorkoutExerciseRequest(entry.ExerciseId, entry.TargetSets))]);

            (await client.PutAsJsonAsync($"/api/workouts/{id}/exercises", request)).EnsureSuccessStatusCode();
        }

        return id;
    }
}
