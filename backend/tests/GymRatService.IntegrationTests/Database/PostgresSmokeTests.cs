using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Filtering;

namespace GymRatService.IntegrationTests.Database;

[Trait("Category", "Postgres")]
public sealed class PostgresSmokeTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Register_StoresTheUserAndTheProfileCanBeRead()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@example.com";
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", "Pop", WeightUnit.Pounds));

        // Act
        var profile = await _factory.CreateBearerClient(accessToken).GetFromJsonAsync<UserProfileDto>("/api/users/me");

        // Assert
        Assert.Equal(new UserProfileDto("Ana", "Pop", email, WeightUnit.Pounds, 90), profile);
    }

    [Fact]
    public async Task GetExercises_ReturnsTheCatalogSeededByLiquibase()
    {
        // Arrange
        var client = await CreateClientAsync();

        // Act
        var page = await client.GetFromJsonAsync<PagedResult<ExerciseDto>>("/api/exercises?pagination.pageSize=100");

        // Assert
        Assert.NotNull(page);
        Assert.True(page.TotalCount >= 89);
        Assert.All(page.Items, item => Assert.Equal(7, item.Id.Version));
    }

    [Fact]
    public async Task WorkoutSession_CanBeStartedLoggedAndFinished()
    {
        // Arrange
        var client = await CreateClientAsync();
        var exerciseId = await FirstExerciseIdAsync(client);
        var workoutId = await CreateWorkoutAsync(client, exerciseId);

        // Act
        var started = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var sessionId = (await started.Content.ReadFromJsonAsync<StartSessionResponse>())!.Id;
        var saved = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(8, 62.5m), new SessionSetRequest(6, 65m, 2)]));
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");
        var finished = await client.PostAsync($"/api/sessions/{sessionId}/finish", null);
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{exerciseId}/progress");

        // Assert
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(sessionId, active?.SessionId);
        Assert.Equal(HttpStatusCode.NoContent, finished.StatusCode);
        var day = Assert.Single(progress!);
        Assert.Equal(new BestSetDto(8, 62.5m), day.BestSet);
    }

    [Fact]
    public async Task StartSession_ReturnsTheOpenSession_WhenTheSameWorkoutIsStartedTwice()
    {
        // Arrange
        var client = await CreateClientAsync();
        var workoutId = await CreateWorkoutAsync(client, await FirstExerciseIdAsync(client));
        var first = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));

        // Act
        var second = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));

        // Assert
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<StartSessionResponse>())!.Id;
        var secondId = (await second.Content.ReadFromJsonAsync<StartSessionResponse>())!.Id;
        Assert.Equal(firstId, secondId);
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }

    private static async Task<Guid> FirstExerciseIdAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<ExerciseDto>>("/api/exercises?pagination.pageSize=1");

        return page!.Items[0].Id;
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, Guid exerciseId)
    {
        var created = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest("Push"));
        var workoutId = (await created.Content.ReadFromJsonAsync<CreateWorkoutResponse>())!.Id;
        var replaced = await client.PutAsJsonAsync(
            $"/api/workouts/{workoutId}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(exerciseId, 3)]));
        replaced.EnsureSuccessStatusCode();

        return workoutId;
    }
}
