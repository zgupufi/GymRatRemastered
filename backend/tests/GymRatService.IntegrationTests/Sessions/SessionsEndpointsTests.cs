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
    [InlineData("GET", "/api/sessions/active")]
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
    public async Task GetActiveSession_ReturnsTheUnfinishedSessionWithItsSavedSets()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(8, 60, 2), new SessionSetRequest(6, 62.5m));

        // Act
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");

        // Assert
        Assert.NotNull(active);
        Assert.Equal(sessionId, active.SessionId);
        Assert.Equal(workoutId, active.WorkoutId);
        Assert.NotNull(active.StartedAt);
        var exercise = Assert.Single(active.Exercises);
        Assert.Equal(bench.Id, exercise.ExerciseId);
        Assert.Equal([new SessionSetDto(1, 8, 60, 2), new SessionSetDto(2, 6, 62.5m)], exercise.Sets);
    }

    [Fact]
    public async Task StartSession_ReturnsTheSameId_WhenTheWorkoutAlreadyHasAnOpenSession()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var first = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, first, bench.Id, new SessionSetRequest(8, 60));

        // Act
        var second = await StartSessionAsync(client, workoutId);

        // Assert
        Assert.Equal(first, second);
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");
        Assert.Equal(first, active?.SessionId);
        Assert.Equal([new SessionSetDto(1, 8, 60)], Assert.Single(active!.Exercises).Sets);
    }

    [Fact]
    public async Task StartSession_FinishesTheOpenSessionWithSets_WhenAnotherWorkoutIsStarted()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var pushId = await CreateWorkoutAsync(client, "Push");
        var pullId = await CreateWorkoutAsync(client, "Pull");
        var push = await StartSessionAsync(client, pushId);
        await SaveSetsAsync(client, push, bench.Id, new SessionSetRequest(8, 60));

        // Act
        var pull = await StartSessionAsync(client, pullId);

        // Assert
        Assert.NotEqual(push, pull);
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{pushId}/last-session");
        Assert.Equal(push, last?.SessionId);
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");
        Assert.Null(active?.SessionId);
    }

    [Fact]
    public async Task StartSession_ReturnsANewId_WhenThePreviousSessionWasFinished()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var first = await StartSessionAsync(client, workoutId);
        (await client.PostAsync($"/api/sessions/{first}/finish", null)).EnsureSuccessStatusCode();

        // Act
        var second = await StartSessionAsync(client, workoutId);

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task GetActiveSession_IgnoresUnfinishedSessionsWithoutSavedSets()
    {
        // Arrange
        await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        await StartSessionAsync(client, workoutId);

        // Act
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");

        // Assert
        Assert.Null(active?.SessionId);
        Assert.Empty(active!.Exercises);
    }

    [Fact]
    public async Task GetActiveSession_ReturnsAnEmptyResult_WhenTheWorkoutWasDeleted()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(8, 60));
        (await client.DeleteAsync($"/api/workouts/{workoutId}")).EnsureSuccessStatusCode();

        // Act
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");

        // Assert
        Assert.Null(active?.SessionId);
        Assert.Empty(active!.Exercises);
    }

    [Fact]
    public async Task GetActiveSession_ReturnsAnEmptyResult_WhenTheSessionWasFinished()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(8, 60));
        (await client.PostAsync($"/api/sessions/{sessionId}/finish", null)).EnsureSuccessStatusCode();

        // Act
        var active = await client.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");

        // Assert
        Assert.Null(active?.SessionId);
        Assert.Null(active?.WorkoutId);
        Assert.Null(active?.StartedAt);
        Assert.Empty(active!.Exercises);
    }

    [Fact]
    public async Task GetActiveSession_DoesNotReturnTheSessionOfAnotherUser()
    {
        // Arrange
        await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var workoutId = await CreateWorkoutAsync(owner, "Push");
        await StartSessionAsync(owner, workoutId);

        // Act
        var active = await intruder.GetFromJsonAsync<ActiveSessionDto>("/api/sessions/active");

        // Assert
        Assert.Null(active?.SessionId);
        Assert.Empty(active!.Exercises);
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
        await _factory.SeedAsync(context => context.WorkoutSessions.Single(session => session.Id == sessionId).StartedAt = DateTimeOffset.UtcNow.AddMinutes(-45));

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
        await _factory.SeedAsync(context => context.WorkoutSessions.Single(session => session.Id == dayOne).StartedAt = DateTimeOffset.UtcNow.AddDays(-2));
        var dayTwo = await StartSessionAsync(ana, workoutId);
        await SaveSetsAsync(ana, dayTwo, bench.Id, new SessionSetRequest(1, 105));
        await ana.PostAsync($"/api/sessions/{dayTwo}/finish", null);

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
    public async Task GetExerciseProgress_BucketsTheSessionByTheCallersTimeZone_WhenItStartedJustAfterLocalMidnight()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var lateEvening = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-10).AddHours(22).AddMinutes(30), TimeSpan.Zero);
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(5, 100));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);
        await _factory.SeedAsync(context => context.WorkoutSessions.Single(session => session.Id == sessionId).StartedAt = lateEvening);

        // Act
        var utc = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");
        var empty = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress?timeZone=");
        var bucharest = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress?timeZone=Europe%2FBucharest");

        // Assert
        Assert.Equal(DateOnly.FromDateTime(lateEvening.UtcDateTime), Assert.Single(utc!).Date);
        Assert.Equal(DateOnly.FromDateTime(lateEvening.UtcDateTime), Assert.Single(empty!).Date);
        Assert.Equal(DateOnly.FromDateTime(lateEvening.UtcDateTime).AddDays(1), Assert.Single(bucharest!).Date);
    }

    [Fact]
    public async Task GetExerciseProgress_IgnoresSessionsThatWereNeverFinished()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var unfinished = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, unfinished, bench.Id, new SessionSetRequest(5, 200));

        // Act
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        Assert.Empty(progress!);
    }

    [Fact]
    public async Task GetExerciseProgress_PicksTheBestSetByEstimatedOneRepMax()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, bench.Id, new SessionSetRequest(1, 110), new SessionSetRequest(10, 85));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Act
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        var day = Assert.Single(progress!);
        Assert.Equal(new BestSetDto(10, 85), day.BestSet);
        Assert.Equal(113.33m, day.EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgress_DefaultsToTheLastYear_AndHonoursTheRequestedRange()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var recent = await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(5, 100));
        var sixMonths = await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(5, 90));
        var twoYears = await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(5, 80));
        await _factory.SeedAsync(context =>
        {
            context.WorkoutSessions.Single(session => session.Id == recent).StartedAt = DateTimeOffset.UtcNow.AddDays(-20);
            context.WorkoutSessions.Single(session => session.Id == sixMonths).StartedAt = DateTimeOffset.UtcNow.AddMonths(-6);
            context.WorkoutSessions.Single(session => session.Id == twoYears).StartedAt = DateTimeOffset.UtcNow.AddYears(-2);
        });

        // Act
        var byDefault = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");
        var threeMonths = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress?range=ThreeMonths");
        var oneYear = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress?range=OneYear");
        var all = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress?range=All");

        // Assert
        Assert.Equal([80m, 90m, 100m], all!.Select(point => point.BestSet.WeightKg).Order());
        Assert.Equal([90m, 100m], byDefault!.Select(point => point.BestSet.WeightKg));
        Assert.Equal([90m, 100m], oneYear!.Select(point => point.BestSet.WeightKg));
        Assert.Equal([100m], threeMonths!.Select(point => point.BestSet.WeightKg));
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsOneBestSetPerDay_WhenTwoSessionsShareTheDay()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        var morning = await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(5, 100), new SessionSetRequest(5, 100), new SessionSetRequest(8, 70));
        var evening = await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(3, 110));
        var day = DateTimeOffset.UtcNow.Date.AddDays(-3);
        await _factory.SeedAsync(context =>
        {
            context.WorkoutSessions.Single(session => session.Id == morning).StartedAt = new DateTimeOffset(day.AddHours(8), TimeSpan.Zero);
            context.WorkoutSessions.Single(session => session.Id == evening).StartedAt = new DateTimeOffset(day.AddHours(18), TimeSpan.Zero);
        });

        // Act
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        var point = Assert.Single(progress!);
        Assert.Equal(DateOnly.FromDateTime(day), point.Date);
        Assert.Equal(new BestSetDto(3, 110), point.BestSet);
        Assert.Equal(121m, point.EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsASingleBestSet_WhenSeveralSetsAreIdentical()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(5, 100), new SessionSetRequest(5, 100), new SessionSetRequest(5, 100));

        // Act
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        var point = Assert.Single(progress!);
        Assert.Equal(new BestSetDto(5, 100), point.BestSet);
        Assert.Equal(116.67m, point.EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgress_PicksTheHeavierSet_WhenTwoSetsHaveTheSameEstimatedOneRepMax()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push");
        await FinishSessionAsync(client, workoutId, bench.Id, new SessionSetRequest(15, 80), new SessionSetRequest(6, 100));

        // Act
        var progress = await client.GetFromJsonAsync<List<ExerciseProgressDto>>($"/api/exercises/{bench.Id}/progress");

        // Assert
        var point = Assert.Single(progress!);
        Assert.Equal(new BestSetDto(6, 100), point.BestSet);
        Assert.Equal(120m, point.EstimatedOneRepMax);
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsBadRequest_WhenTheRangeIsUnknown()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.GetAsync($"/api/exercises/{bench.Id}/progress?range=42");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetExerciseProgress_ReturnsBadRequest_WhenTheTimeZoneIsUnknown()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.GetAsync($"/api/exercises/{bench.Id}/progress?timeZone=Mars%2FOlympus");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static async Task<Guid> FinishSessionAsync(HttpClient client, Guid workoutId, Guid exerciseId, params SessionSetRequest[] sets)
    {
        var sessionId = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, sessionId, exerciseId, sets);
        var finish = await client.PostAsync($"/api/sessions/{sessionId}/finish", null);
        finish.EnsureSuccessStatusCode();

        return sessionId;
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
