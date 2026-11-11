using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Targets;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Sessions;

public sealed class TargetsEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetTargets_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/workouts/{Guid.CreateVersion7()}/targets");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTargets_ReturnsNoHistoryWithTheRangeAndEquipmentStep_ForANewWorkout()
    {
        // Arrange
        var (bench, curl, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, new WorkoutExerciseRequest(bench.Id, 4), new WorkoutExerciseRequest(curl.Id, 3, 10, 15));

        // Act
        var targets = await client.GetFromJsonAsync<List<ExerciseTargetDto>>($"/api/workouts/{workoutId}/targets");
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");

        // Assert
        Assert.NotNull(targets);
        Assert.Equivalent(
            new[]
            {
                new ExerciseTargetDto(1, bench.Id, 4, 8, 12, null, 8, ProgressionReason.NoHistory, null, []),
                new ExerciseTargetDto(2, curl.Id, 3, 10, 15, null, 10, ProgressionReason.NoHistory, null, []),
            },
            targets,
            strict: true);
        Assert.Equal([2.5m, 2m], workout!.Exercises.Select(entry => entry.WeightStepKg));
    }

    [Fact]
    public async Task GetTargets_AppliesDoubleProgressionToTheLatestFinishedSession()
    {
        // Arrange
        var (bench, curl, squat) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(
            client,
            new WorkoutExerciseRequest(bench.Id, 3),
            new WorkoutExerciseRequest(curl.Id, 3),
            new WorkoutExerciseRequest(squat.Id, 3, 5, 8));
        await CompleteSessionAsync(client, workoutId, new()
        {
            [bench.Id] = [new(8, 60), new(8, 60)],
            [squat.Id] = [new(5, 100, 0)],
        });
        await CompleteSessionAsync(client, workoutId, new()
        {
            [bench.Id] = [new(12, 60), new(12, 60), new(12, 60, 1)],
            [curl.Id] = [new(9, 14), new(8, 14)],
            [squat.Id] = [new(5, 100, 0), new(5, 100, 1)],
        });
        var unfinished = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, unfinished, bench.Id, new SessionSetRequest(1, 20));

        // Act
        var targets = await client.GetFromJsonAsync<List<ExerciseTargetDto>>($"/api/workouts/{workoutId}/targets");

        // Assert
        Assert.NotNull(targets);
        Assert.Equal(
            [
                (62.5m, 8, ProgressionReason.IncreaseWeight),
                (14m, 9, ProgressionReason.AddRep),
                (100m, 5, ProgressionReason.Stalled),
            ],
            targets.Select(target => (target.WeightKg!.Value, target.Reps, target.Reason)));
        Assert.Equal([12, 12, 12], targets[0].LastReps);
        Assert.Equal(60m, targets[0].LastWeightKg);
    }

    [Fact]
    public async Task GetTargets_ReturnsWorkoutNotFound_ForAnotherUsersWorkout()
    {
        // Arrange
        await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var workoutId = await CreateWorkoutAsync(owner);

        // Act
        var response = await intruder.GetAsync($"/api/workouts/{workoutId}/targets");

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.WorkoutNotFound);
    }

    [Fact]
    public async Task ReplaceExercises_KeepsTheSettingsOfEachOccurrence_WhenTheyAreOmitted()
    {
        // Arrange
        var (bench, curl, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(
            client,
            new WorkoutExerciseRequest(bench.Id, 3, 6, 10, 5),
            new WorkoutExerciseRequest(curl.Id, 3),
            new WorkoutExerciseRequest(bench.Id, 2, 12, 15, 1));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/workouts/{workoutId}/exercises",
            new ReplaceWorkoutExercisesRequest(
            [
                new WorkoutExerciseRequest(bench.Id, 4),
                new WorkoutExerciseRequest(bench.Id, 2),
                new WorkoutExerciseRequest(bench.Id, 1),
            ]));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal(
            [(4, 6, 10, 5m), (2, 12, 15, 1m), (1, 8, 12, 2.5m)],
            workout!.Exercises.Select(entry => (entry.TargetSets, entry.RepsMin, entry.RepsMax, entry.WeightStepKg)));
    }

    [Fact]
    public async Task ReplaceExercises_ReturnsValidationErrors_ForAnInvalidRangeOrStep()
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/workouts/{workoutId}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(bench.Id, 3, 12, 8, 3)]));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Equal(["Exercises[0].RepsMax", "Exercises[0].WeightStepKg"], error!.Errors!.Select(item => item.Property).Order());
    }

    [Fact]
    public async Task SaveSets_StoresEffortAndAnyNumberOfSets_AndReturnsThemInTheLastSession()
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, new WorkoutExerciseRequest(bench.Id, 2));
        var sessionId = await StartSessionAsync(client, workoutId);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            new SaveExerciseSetsRequest(
                [new SessionSetRequest(8, 60, 2), new SessionSetRequest(8, 60), new SessionSetRequest(7, 60, 0)],
                new ServedTargetRequest(60, 8, ProgressionReason.AddRep)));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var last = await client.GetFromJsonAsync<LastSessionDto>($"/api/workouts/{workoutId}/last-session");
        Assert.Equal(
            [new SessionSetDto(1, 8, 60, 2), new SessionSetDto(2, 8, 60), new SessionSetDto(3, 7, 60, 0)],
            Assert.Single(last!.Exercises).Sets);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task SaveSets_ReturnsValidationErrors_WhenTheEffortIsOutOfRange(int repsInReserve)
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, new WorkoutExerciseRequest(bench.Id, 2));
        var sessionId = await StartSessionAsync(client, workoutId);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            new SaveExerciseSetsRequest([new SessionSetRequest(8, 60, repsInReserve)]));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Sets[0].RepsInReserve", Assert.Single(error!.Errors!).Property);
    }

    [Fact]
    public async Task Targets_RoundTripForAPoundsUser_AreStoredInKilograms()
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest("lb@example.com", "password123", "Lb", null, WeightUnit.Pounds));
        var client = _factory.CreateBearerClient(accessToken);
        var workoutId = await CreateWorkoutAsync(client, new WorkoutExerciseRequest(bench.Id, 1));
        await CompleteSessionAsync(client, workoutId, new() { [bench.Id] = [new(12, 61.23m)] });

        // Act
        var targets = await client.GetFromJsonAsync<List<ExerciseTargetDto>>($"/api/workouts/{workoutId}/targets");

        // Assert
        var target = Assert.Single(targets!);
        Assert.Equal((62.5m, 61.23m), (target.WeightKg, target.LastWeightKg));
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

    private async Task<(ExerciseCard Bench, ExerciseCard Curl, ExerciseCard Squat)> SeedExercisesAsync()
    {
        var bench = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Equipment = "Barbell" };
        var curl = new ExerciseCard { Name = "Curl", MainMuscle = "Biceps", Equipment = "Dumbbells" };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = "Legs", Equipment = "Barbell" };
        await _factory.SeedAsync(context => context.Exercises.AddRange(bench, curl, squat));

        return (bench, curl, squat);
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, params WorkoutExerciseRequest[] entries)
    {
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest("Push"));
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        var id = created?.Id ?? throw new InvalidOperationException("Create workout did not return an id.");

        if (entries.Length > 0)
        {
            var replace = await client.PutAsJsonAsync($"/api/workouts/{id}/exercises", new ReplaceWorkoutExercisesRequest(entries));
            replace.EnsureSuccessStatusCode();
        }

        return id;
    }

    private static async Task<Guid> StartSessionAsync(HttpClient client, Guid workoutId)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var created = await response.Content.ReadFromJsonAsync<StartSessionResponse>();

        return created?.Id ?? throw new InvalidOperationException("Start session did not return an id.");
    }

    private static async Task SaveSetsAsync(HttpClient client, Guid sessionId, Guid exerciseId, params SessionSetRequest[] sets)
    {
        var response = await client.PutAsJsonAsync($"/api/sessions/{sessionId}/exercises/{exerciseId}/sets", new SaveExerciseSetsRequest(sets));

        response.EnsureSuccessStatusCode();
    }

    private static async Task CompleteSessionAsync(HttpClient client, Guid workoutId, Dictionary<Guid, SessionSetRequest[]> sets)
    {
        var sessionId = await StartSessionAsync(client, workoutId);

        foreach (var (exerciseId, exerciseSets) in sets)
        {
            await SaveSetsAsync(client, sessionId, exerciseId, exerciseSets);
        }

        var finish = await client.PostAsync($"/api/sessions/{sessionId}/finish", null);
        finish.EnsureSuccessStatusCode();
    }
}
