using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Sessions;

public sealed class SessionsEndpointsTests : IDisposable
{
    private const string AnyId = "00000000-0000-0000-0000-000000000001";

    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("POST", "/api/sessions")]
    [InlineData("PUT", $"/api/sessions/{AnyId}/exercises/{AnyId}/sets")]
    [InlineData("POST", $"/api/sessions/{AnyId}/finish")]
    [InlineData("GET", $"/api/workouts/{AnyId}/last-session")]
    [InlineData("GET", $"/api/exercises/{AnyId}/progress")]
    public async Task SessionEndpoints_ReturnUnauthorized_WhenTheCallerIsNotAuthenticated(string method, string url)
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
    public async Task StartSession_ReturnsCreatedWithTheNewId()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");

        // Act
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<StartSessionResponse>();
        Assert.NotNull(created);
        Assert.Equal(7, created.Id.Version);
        Assert.Equal($"/api/sessions/{created.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StartSession_ReturnsBadRequestWithValidationErrors_WhenTheWorkoutIdIsEmpty()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(Guid.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Equal("WorkoutId", Assert.Single(error!.Errors!).Property);
    }

    [Fact]
    public async Task StartSession_ReturnsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var workoutId = await CreateWorkoutAsync(owner, "Push");

        // Act
        var response = await intruder.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.WorkoutNotFound);
    }

    [Fact]
    public async Task SaveSets_ReplacesThePreviousSetsOfThatExerciseOnly()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(8, 60), new SessionSetRequest(8, 60), new SessionSetRequest(6, 62.5m));
        await SaveSetsAsync(client, sessionId, squat.Id, new SessionSetRequest(5, 100));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(10, 55)]));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{workoutId}/last-session");
        Assert.NotNull(last);
        var benchSets = Assert.Single(last.Exercises, exercise => exercise.ExerciseId == bench.Id);
        Assert.Equal([new SessionSetDto(1, 10, 55)], benchSets.Sets);
        var squatSets = Assert.Single(last.Exercises, exercise => exercise.ExerciseId == squat.Id);
        Assert.Equal([new SessionSetDto(1, 5, 100)], squatSets.Sets);
    }

    [Fact]
    public async Task SaveSets_ClearsTheSets_WhenAnEmptyListIsSent()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(8, 60));

        // Act
        await SaveSetsAsync(client, sessionId, bench.Id);
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Assert
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{workoutId}/last-session");
        Assert.Equal(sessionId, last?.SessionId);
        Assert.Empty(last!.Exercises);
    }

    [Fact]
    public async Task SaveSets_ReturnsBadRequestWithValidationErrors_WhenASetIsInvalid()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var sessionId = await StartSessionAsync(client, await CreateWorkoutAsync(client, "Push"));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(0, 60)]));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Equal("Sets[0].Reps", Assert.Single(error!.Errors!).Property);
    }

    [Fact]
    public async Task SaveSets_ReturnsSessionNotFound_WhenTheSessionBelongsToAnotherUser()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var sessionId = await StartSessionAsync(owner, await CreateWorkoutAsync(owner, "Push"));

        // Act
        var response = await intruder.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(8, 60)]));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task SaveSets_ReturnsExerciseNotFound_WhenTheExerciseDoesNotExist()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var sessionId = await StartSessionAsync(client, await CreateWorkoutAsync(client, "Push"));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{Guid.CreateVersion7()}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(8, 60)]));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.ExerciseNotFound);
    }

    [Fact]
    public async Task SaveSets_ReturnsSessionAlreadyFinished_WhenTheSessionIsClosed()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var sessionId = await StartSessionAsync(client, await CreateWorkoutAsync(client, "Push"));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(8, 60)]));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.SessionAlreadyFinished);
    }

    [Fact]
    public async Task FinishSession_StoresTheDurationShownInTheLastSession()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        _factory.Clock.Advance(TimeSpan.FromMinutes(45));

        // Act
        var response = await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{workoutId}/last-session");
        Assert.Equal(sessionId, last?.SessionId);
        Assert.InRange(last!.DurationSeconds!.Value, 2700, 2705);
    }

    [Fact]
    public async Task FinishSession_ReturnsSessionAlreadyFinished_WhenCalledTwice()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var sessionId = await StartSessionAsync(client, await CreateWorkoutAsync(client, "Push"));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Act
        var response = await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.SessionAlreadyFinished);
    }

    [Fact]
    public async Task FinishSession_ReturnsSessionNotFound_WhenTheSessionBelongsToAnotherUser()
    {
        // Arrange
        await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var sessionId = await StartSessionAsync(owner, await CreateWorkoutAsync(owner, "Push"));

        // Act
        var response = await intruder.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task GetLastSession_ReturnsAnEmptySession_WhenTheWorkoutWasNeverFinished()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        await StartSessionAsync(client, workoutId);

        // Act
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{workoutId}/last-session");

        // Assert
        Assert.NotNull(last);
        Assert.Null(last.SessionId);
        Assert.Null(last.DurationSeconds);
        Assert.Empty(last.Exercises);
    }

    [Fact]
    public async Task GetLastSession_ReturnsTheMostRecentFinishedSession()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var first = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, first, bench.Id, new SessionSetRequest(8, 60));
        await client.PostAsync($"/api/sessions/{first}/finish", null);
        var second = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, second, bench.Id, new SessionSetRequest(6, 65), new SessionSetRequest(5, 67.5m));
        await client.PostAsync($"/api/sessions/{second}/finish", null);
        var open = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, open, bench.Id, new SessionSetRequest(1, 200));

        // Act
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{workoutId}/last-session");

        // Assert
        Assert.NotNull(last);
        Assert.Equal(second, last.SessionId);
        var exercise = Assert.Single(last.Exercises);
        Assert.Equal(bench.Id, exercise.ExerciseId);
        Assert.Equal([new SessionSetDto(1, 6, 65), new SessionSetDto(2, 5, 67.5m)], exercise.Sets);
    }

    [Fact]
    public async Task GetLastSession_ReturnsWorkoutNotFound_WhenTheWorkoutBelongsToAnotherUser()
    {
        // Arrange
        await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var workoutId = await CreateWorkoutAsync(owner, "Push");

        // Act
        var response = await intruder.GetAsync($"/api/workouts/{workoutId}/last-session");

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.WorkoutNotFound);
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsTheBestSetAndEpleyEstimatePerDayFromTheCallersSessionsOnly()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var ana = await CreateClientAsync("ana@example.com");
        var other = await CreateClientAsync("other@example.com");
        var workoutId = await CreateWorkoutAsync(ana, "Push");
        var otherWorkoutId = await CreateWorkoutAsync(other, "Push");
        var dayOne = await StartSessionAsync(ana, workoutId);
        await SaveSetsAsync(ana, dayOne, bench.Id, new SessionSetRequest(10, 80), new SessionSetRequest(5, 100), new SessionSetRequest(3, 100));
        await ana.PostAsync($"/api/sessions/{dayOne}/finish", null);
        var otherSession = await StartSessionAsync(other, otherWorkoutId);
        await SaveSetsAsync(other, otherSession, bench.Id, new SessionSetRequest(1, 300));
        _factory.Clock.Advance(TimeSpan.FromDays(2));
        var dayTwo = await StartSessionAsync(ana, workoutId);
        await SaveSetsAsync(ana, dayTwo, bench.Id, new SessionSetRequest(1, 105));

        // Act
        var progress = await ana.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        Assert.NotNull(progress);
        Assert.Equal(2, progress.Count);
        Assert.Equal(new BestSetDto(5, 100), progress[0].BestSet);
        Assert.Equal(116.67m, progress[0].EstimatedOneRepMax);
        Assert.Equal(progress[0].Date.AddDays(2), progress[1].Date);
        Assert.Equal(new BestSetDto(1, 105), progress[1].BestSet);
        Assert.Equal(105m, progress[1].EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgress_KeepsTheHistory_WhenTheWorkoutIsDeleted()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(8, 60));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Act
        var deleted = await client.DeleteAsync($"/api/workouts/{workoutId}");
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var day = Assert.Single(progress!);
        Assert.Equal(new BestSetDto(8, 60), day.BestSet);
        Assert.Equal(76m, day.EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsAnEmptyList_WhenTheCallerNeverTrainedTheExercise()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        Assert.Empty(progress!);
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsExerciseNotFound_WhenTheExerciseDoesNotExist()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.GetAsync($"/api/exercises/{Guid.CreateVersion7()}/progress");

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.ExerciseNotFound);
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(code, error?.Code);
    }

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }

    private async Task<(ExerciseCard Bench, ExerciseCard Squat)> SeedExercisesAsync()
    {
        var bench = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest" };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = "Legs" };
        await _factory.SeedAsync(context => context.Exercises.AddRange(bench, squat));

        return (bench, squat);
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest(name));
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();

        return created?.Id ?? throw new InvalidOperationException("Create workout did not return an id.");
    }

    private static async Task<Guid> StartSessionAsync(HttpClient client, Guid workoutId)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var created = await response.Content.ReadFromJsonAsync<StartSessionResponse>();

        return created?.Id ?? throw new InvalidOperationException("Start session did not return an id.");
    }

    private static async Task SaveSetsAsync(HttpClient client, Guid sessionId, Guid exerciseId, params SessionSetRequest[] sets)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/sets",
            new SaveExerciseSetsRequest(sets));

        response.EnsureSuccessStatusCode();
    }
}
