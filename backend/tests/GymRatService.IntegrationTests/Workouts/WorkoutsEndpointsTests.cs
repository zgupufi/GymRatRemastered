using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;

namespace GymRatService.IntegrationTests.Workouts;

public sealed class WorkoutsEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("GET", "/api/workouts")]
    [InlineData("POST", "/api/workouts")]
    [InlineData("GET", "/api/workouts/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/workouts/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/workouts/00000000-0000-0000-0000-000000000001/exercises")]
    [InlineData("DELETE", "/api/workouts/00000000-0000-0000-0000-000000000001")]
    public async Task Workouts_ReturnUnauthorized_WhenTheCallerIsNotAuthenticated(string method, string url)
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
    public async Task CreateWorkout_ReturnsCreatedWithTheNewIdAndTheWorkoutCanBeRead()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest(" Push Day "));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        Assert.NotNull(created);
        Assert.Equal(7, created.Id.Version);
        Assert.Equal($"/api/workouts/{created.Id}", response.Headers.Location?.OriginalString);

        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{created.Id}");
        Assert.NotNull(workout);
        Assert.Equal(created.Id, workout.Id);
        Assert.Equal("Push Day", workout.Name);
        Assert.Empty(workout.Exercises);
    }

    [Fact]
    public async Task CreateWorkout_ReturnsBadRequestWithValidationErrors_WhenTheNameIsEmpty()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error.Code);
        Assert.Equal("Name", Assert.Single(error.Errors!).Property);
    }

    [Fact]
    public async Task GetWorkoutSummaries_ReturnsOnlyTheCallersWorkoutsWithCountsAndMainMuscles()
    {
        // Arrange
        var (bench, squat, _) = await SeedExercisesAsync();
        var ana = await CreateClientAsync("ana@example.com");
        var other = await CreateClientAsync("other@example.com");
        var push = await CreateWorkoutAsync(ana, "Push");
        var legs = await CreateWorkoutAsync(ana, "Legs");
        await CreateWorkoutAsync(other, "Other");
        await ReplaceExercisesAsync(ana, push, (bench.Id, 3), (bench.Id, 4), (squat.Id, 5));
        await ReplaceExercisesAsync(ana, legs, (squat.Id, 3));

        // Act
        var response = await ana.GetAsync("/api/workouts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summaries = await response.Content.ReadFromJsonAsync<List<WorkoutSummaryDto>>();
        Assert.NotNull(summaries);
        Assert.Collection(
            summaries,
            first =>
            {
                Assert.Equal(push, first.Id);
                Assert.Equal("Push", first.Name);
                Assert.Equal(3, first.ExerciseCount);
                Assert.Equal(["Chest", "Legs"], first.MainMuscles);
            },
            second =>
            {
                Assert.Equal(legs, second.Id);
                Assert.Equal("Legs", second.Name);
                Assert.Equal(1, second.ExerciseCount);
                Assert.Equal(["Legs"], second.MainMuscles);
            });
    }

    [Fact]
    public async Task GetWorkout_ReturnsTheExercisesInTheOrderTheyWereReplacedWith()
    {
        // Arrange
        var (bench, squat, deadlift) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Full Body");
        await ReplaceExercisesAsync(client, id, (squat.Id, 5), (deadlift.Id, 1), (bench.Id, 4));

        // Act
        var response = await client.GetAsync($"/api/workouts/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var workout = await response.Content.ReadFromJsonAsync<WorkoutDto>();
        Assert.NotNull(workout);
        Assert.Equal("Full Body", workout.Name);
        Assert.Equal([1, 2, 3], workout.Exercises.Select(entry => entry.Order));
        Assert.Equal([5, 1, 4], workout.Exercises.Select(entry => entry.TargetSets));
        Assert.Equal(
            [
                new ExerciseDto(squat.Id, "Squat", "Legs", "Glutes", "Barbell", null, null, null),
                new ExerciseDto(deadlift.Id, "Deadlift", "Back", null, null, null, null, null),
                new ExerciseDto(bench.Id, "Bench Press", "Chest", null, null, "Intermediate", null, null),
            ],
            workout.Exercises.Select(entry => entry.Exercise));
    }

    [Theory]
    [InlineData("en", "Workout {0} does not exist.")]
    [InlineData("ro", "Antrenamentul {0} nu există.")]
    public async Task GetWorkout_ReturnsBadRequestWithWorkoutNotFound_WhenItBelongsToAnotherUser(string language, string expectedMessageFormat)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var id = await CreateWorkoutAsync(owner, "Private");
        intruder.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await intruder.GetAsync($"/api/workouts/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.WorkoutNotFound, string.Format(expectedMessageFormat, id)), error);
    }

    [Fact]
    public async Task GetWorkout_ReturnsBadRequestWithWorkoutNotFound_WhenItDoesNotExist()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await client.GetAsync($"/api/workouts/{missingId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.WorkoutNotFound, error?.Code);
    }

    [Fact]
    public async Task RenameWorkout_ReturnsNoContentAndTheNewNameIsReturned()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");

        // Act
        var response = await client.PatchAsJsonAsync($"/api/workouts/{id}", new RenameWorkoutRequest(" Pull "));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        Assert.Equal("Pull", workout?.Name);
    }

    [Fact]
    public async Task RenameWorkout_ReturnsBadRequestAndKeepsTheName_WhenItBelongsToAnotherUser()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var id = await CreateWorkoutAsync(owner, "Push");

        // Act
        var response = await intruder.PatchAsJsonAsync($"/api/workouts/{id}", new RenameWorkoutRequest("Hacked"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.WorkoutNotFound, error?.Code);
        var workout = await owner.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        Assert.Equal("Push", workout?.Name);
    }

    [Fact]
    public async Task RenameWorkout_ReturnsBadRequestWithValidationErrors_WhenTheNameIsTooLong()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");

        // Act
        var response = await client.PatchAsJsonAsync($"/api/workouts/{id}", new RenameWorkoutRequest(new string('w', 101)));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Equal("Name", Assert.Single(error!.Errors!).Property);
    }

    [Fact]
    public async Task ReplaceWorkoutExercises_ReplacesThePreviousListAndReturnsNoContent()
    {
        // Arrange
        var (bench, squat, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");
        await ReplaceExercisesAsync(client, id, (bench.Id, 3), (squat.Id, 3));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/workouts/{id}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(squat.Id, 6)]));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        var entry = Assert.Single(workout!.Exercises);
        Assert.Equal(1, entry.Order);
        Assert.Equal(6, entry.TargetSets);
        Assert.Equal(squat.Id, entry.Exercise.Id);
    }

    [Fact]
    public async Task ReplaceWorkoutExercises_ClearsTheList_WhenTheRequestIsEmpty()
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");
        await ReplaceExercisesAsync(client, id, (bench.Id, 3));

        // Act
        var response = await client.PutAsJsonAsync($"/api/workouts/{id}/exercises", new ReplaceWorkoutExercisesRequest([]));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        Assert.Empty(workout!.Exercises);
    }

    [Theory]
    [InlineData("en", "One or more of the exercises do not exist.")]
    [InlineData("ro", "Unul sau mai multe exerciții nu există.")]
    public async Task ReplaceWorkoutExercises_ReturnsBadRequestWithExerciseNotFoundAndKeepsTheList_WhenAnExerciseIsMissing(
        string language,
        string expectedMessage)
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");
        await ReplaceExercisesAsync(client, id, (bench.Id, 3));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/workouts/{id}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(bench.Id, 3), new WorkoutExerciseRequest(Guid.CreateVersion7(), 3)]));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(new ErrorResponse(ErrorCodes.WorkoutExerciseNotFound, expectedMessage), error);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        Assert.Equal(bench.Id, Assert.Single(workout!.Exercises).Exercise.Id);
    }

    [Fact]
    public async Task ReplaceWorkoutExercises_ReturnsBadRequestWithValidationErrors_WhenTargetSetsAreOutOfRange()
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/workouts/{id}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(bench.Id, 0)]));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.InvalidRequest, error?.Code);
        Assert.Equal("Exercises[0].TargetSets", Assert.Single(error!.Errors!).Property);
    }

    [Fact]
    public async Task ReplaceWorkoutExercises_ReturnsBadRequestAndKeepsTheList_WhenItBelongsToAnotherUser()
    {
        // Arrange
        var (bench, squat, _) = await SeedExercisesAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var id = await CreateWorkoutAsync(owner, "Push");
        await ReplaceExercisesAsync(owner, id, (bench.Id, 3));

        // Act
        var response = await intruder.PutAsJsonAsync(
            $"/api/workouts/{id}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(squat.Id, 3)]));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.WorkoutNotFound, error?.Code);
        var workout = await owner.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        Assert.Equal(bench.Id, Assert.Single(workout!.Exercises).Exercise.Id);
    }

    [Fact]
    public async Task DeleteWorkout_ReturnsNoContentAndTheWorkoutIsGone()
    {
        // Arrange
        var (bench, _, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var id = await CreateWorkoutAsync(client, "Push");
        var keep = await CreateWorkoutAsync(client, "Keep");
        await ReplaceExercisesAsync(client, id, (bench.Id, 3));

        // Act
        var response = await client.DeleteAsync($"/api/workouts/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var summaries = await client.GetFromJsonAsync<List<WorkoutSummaryDto>>("/api/workouts");
        Assert.Equal([keep], summaries!.Select(summary => summary.Id));
        var read = await client.GetAsync($"/api/workouts/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, read.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkout_ReturnsBadRequestAndKeepsTheWorkout_WhenItBelongsToAnotherUser()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var id = await CreateWorkoutAsync(owner, "Push");

        // Act
        var response = await intruder.DeleteAsync($"/api/workouts/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.WorkoutNotFound, error?.Code);
        var workout = await owner.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{id}");
        Assert.Equal("Push", workout?.Name);
    }

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }

    private async Task<(ExerciseCard Bench, ExerciseCard Squat, ExerciseCard Deadlift)> SeedExercisesAsync()
    {
        var bench = new ExerciseCard { Name = "Bench Press", MainMuscle = "Chest", Difficulty = "Intermediate" };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = "Legs", SubMuscles = "Glutes", Equipment = "Barbell" };
        var deadlift = new ExerciseCard { Name = "Deadlift", MainMuscle = "Back" };
        await _factory.SeedAsync(context => context.Exercises.AddRange(bench, squat, deadlift));

        return (bench, squat, deadlift);
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest(name));
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();

        return created?.Id ?? throw new InvalidOperationException("Create workout did not return an id.");
    }

    private static async Task ReplaceExercisesAsync(HttpClient client, Guid workoutId, params (Guid ExerciseId, int TargetSets)[] entries)
    {
        var request = new ReplaceWorkoutExercisesRequest(
            [.. entries.Select(entry => new WorkoutExerciseRequest(entry.ExerciseId, entry.TargetSets))]);

        var response = await client.PutAsJsonAsync($"/api/workouts/{workoutId}/exercises", request);

        response.EnsureSuccessStatusCode();
    }
}
