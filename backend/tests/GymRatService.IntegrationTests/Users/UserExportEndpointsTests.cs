using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.DTOs.Export;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Split;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.IntegrationTests.Chat;
using GymRatService.Repository.Entities;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.IntegrationTests.Users;

public sealed class UserExportEndpointsTests : IDisposable
{
    private const string CsvHeader = "Date,Workout,Exercise,Set,Reps,Weight (kg),Reps in reserve,Target weight (kg),Target reps,Target reason,Session id";

    private readonly GymRatApiFactory _factory = new();
    private readonly ScriptedChatClient _chat = new();

    public UserExportEndpointsTests()
    {
        _factory.ChatClient = _chat;
        _factory.Settings["AiCoach:MaximumToolIterations"] = "8";
        _factory.Settings["AiCoach:RateLimitRequests"] = "20";
    }

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("/api/users/me/export")]
    [InlineData("/api/users/me/export/sets")]
    public async Task Export_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated(string url)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/users/me/export")]
    [InlineData("/api/users/me/export/sets")]
    public async Task Export_ReturnsBadRequestWithUserNotFound_WhenTheTokenBelongsToAMissingUser(string url)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var accessToken = GymRatApiFactory.CreateAccessToken(new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()));
        var client = _factory.CreateBearerClient(accessToken);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Content.Headers.ContentDisposition is not null);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.UserNotFound, error?.Code);
    }

    [Theory]
    [InlineData("/api/users/me/export")]
    [InlineData("/api/users/me/export/sets")]
    public async Task Export_IsNotCacheable(string url)
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task GetUserExport_ReturnsEmptyCollections_WhenTheUserHasNoData()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var before = DateTimeOffset.UtcNow;

        // Act
        var export = await client.GetFromJsonAsync<UserExportDto>("/api/users/me/export");

        // Assert
        Assert.NotNull(export);
        Assert.InRange(export.ExportedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(new UserProfileDto("Ana", null, "ana@example.com", WeightUnit.Kilograms, 90), export.Profile);
        Assert.Empty(export.Workouts);
        Assert.Empty(export.Split);
        Assert.Empty(export.Sessions);
        Assert.Empty(export.CoachSuggestions);
    }

    [Fact]
    public async Task GetUserExport_ReturnsEveryWorkoutSplitDaySetAndSuggestionOfTheCaller_AndNothingOfOthers()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var other = await CreateClientAsync("other@example.com");
        var otherWorkout = await CreateWorkoutAsync(other, "Other Day", bench.Id);
        var otherSession = await StartSessionAsync(other, otherWorkout);
        await SaveSetsAsync(other, otherSession, bench.Id, new SessionSetRequest(5, 100));
        _chat.Script = Proposal(otherWorkout, bench.Id, new { targetSets = 5 });
        await SendChatAsync(other);
        var client = await CreateClientAsync("ana@example.com");
        var pushId = await CreateWorkoutAsync(client, "Push", bench.Id, squat.Id);
        var pullId = await CreateWorkoutAsync(client, "Pull", squat.Id);
        (await client.PutAsJsonAsync("/api/split/1", new AssignSplitDayRequest(pushId))).EnsureSuccessStatusCode();
        var pushSession = await StartSessionAsync(client, pushId);
        await SaveSetsAsync(
            client,
            pushSession,
            bench.Id,
            new ServedTargetRequest(60, 8, ProgressionReason.AddRep),
            new SessionSetRequest(8, 60, 2),
            new SessionSetRequest(6, 62.5m));
        await SaveSetsAsync(client, pushSession, squat.Id, new SessionSetRequest(5, 100));
        (await client.PostAsync($"/api/sessions/{pushSession}/finish", null)).EnsureSuccessStatusCode();
        var pullSession = await StartSessionAsync(client, pullId);
        await SaveSetsAsync(client, pullSession, squat.Id, new SessionSetRequest(10, 80));
        (await client.DeleteAsync($"/api/workouts/{pullId}")).EnsureSuccessStatusCode();
        _chat.Script = Proposal(pushId, bench.Id, new { targetSets = 2, weightStepKg = 5 });
        var card = Assert.Single((await SendChatAsync(client)).Suggestions);

        // Act
        var export = await client.GetFromJsonAsync<UserExportDto>("/api/users/me/export");

        // Assert
        Assert.NotNull(export);
        var workout = Assert.Single(export.Workouts);
        Assert.Equal(pushId, workout.Id);
        Assert.Equal("Push", workout.Name);
        Assert.Equal(["Bench Press", "Squat"], workout.Exercises.Select(entry => entry.ExerciseName));
        Assert.Equal([1, 2], workout.Exercises.Select(entry => entry.Position));
        var splitDay = Assert.Single(export.Split);
        Assert.Equal(new ExportSplitDayDto(1, pushId, "Push"), splitDay);
        Assert.Equal(2, export.Sessions.Count);
        var finished = export.Sessions.Single(session => session.Id == pushSession);
        Assert.Equal(pushId, finished.WorkoutId);
        Assert.Equal("Push", finished.WorkoutName);
        Assert.NotNull(finished.FinishedAt);
        Assert.Equal(
            [("Bench Press", 1, 8, 60m, 2, 60m, 8, ProgressionReason.AddRep), ("Bench Press", 2, 6, 62.5m, null, 60m, 8, ProgressionReason.AddRep), ("Squat", 1, 5, 100m, null, null, null, null)],
            finished.Sets.Select(set => (set.ExerciseName, set.SetNumber, set.Reps, set.WeightKg, set.RepsInReserve, set.TargetWeightKg, set.TargetReps, set.TargetReason)));
        var orphaned = export.Sessions.Single(session => session.Id == pullSession);
        Assert.Null(orphaned.WorkoutId);
        Assert.Null(orphaned.WorkoutName);
        Assert.Equal(10, Assert.Single(orphaned.Sets).Reps);
        Assert.DoesNotContain(export.Sessions, session => session.Id == otherSession);
        var suggestion = Assert.Single(export.CoachSuggestions);
        Assert.Equal(
            (card.Id, pushId, "Push", bench.Id, "Bench Press", (int?)2, (decimal?)5m, 3, 8, 12, 2.5m, "Stalled.", SuggestionStatus.Pending),
            (suggestion.Id, suggestion.WorkoutId, suggestion.WorkoutName, suggestion.ExerciseId, suggestion.ExerciseName, suggestion.TargetSets, suggestion.WeightStepKg, suggestion.BeforeTargetSets, suggestion.BeforeRepsMin, suggestion.BeforeRepsMax, suggestion.BeforeWeightStepKg, suggestion.Rationale, suggestion.Status));
        Assert.Null(suggestion.ResolvedAt);
    }

    [Fact]
    public async Task GetSetsCsv_ReturnsOneRowPerLoggedSetThatOpensInASpreadsheet()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var other = await CreateClientAsync("other@example.com");
        var otherWorkout = await CreateWorkoutAsync(other, "Other Day", bench.Id);
        var otherSession = await StartSessionAsync(other, otherWorkout);
        await SaveSetsAsync(other, otherSession, bench.Id, new SessionSetRequest(5, 100));
        var client = await CreateClientAsync("ana@example.com");
        var pushId = await CreateWorkoutAsync(client, "Push", bench.Id, squat.Id);
        var session = await StartSessionAsync(client, pushId);
        await SaveSetsAsync(
            client,
            session,
            bench.Id,
            new ServedTargetRequest(60, 8, ProgressionReason.AddRep),
            new SessionSetRequest(8, 60, 2),
            new SessionSetRequest(6, 62.5m));
        await SaveSetsAsync(client, session, squat.Id, new SessionSetRequest(5, 100));

        // Act
        var response = await client.GetAsync("/api/users/me/export/sets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("gymrat-sets.csv", response.Content.Headers.ContentDisposition?.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(CsvHeader, lines[0]);
        Assert.Equal(4, lines.Length);
        var rows = lines[1..].Select(line => line.Split(',')).ToList();
        Assert.Equal(["Push", "Push", "Push"], rows.Select(row => row[1]));
        Assert.Equal(["Bench Press", "Bench Press", "Squat"], rows.Select(row => row[2]));
        Assert.Equal(["1", "2", "1"], rows.Select(row => row[3]));
        Assert.Equal(["8", "6", "5"], rows.Select(row => row[4]));
        Assert.Equal(["60", "62.5", "100"], rows.Select(row => row[5]));
        Assert.Equal(["2", "", ""], rows.Select(row => row[6]));
        Assert.Equal(["60", "60", ""], rows.Select(row => row[7]));
        Assert.Equal(["8", "8", ""], rows.Select(row => row[8]));
        Assert.Equal(["AddRep", "AddRep", ""], rows.Select(row => row[9]));
        Assert.All(rows, row => Assert.Equal(session.ToString(), row[10]));
    }

    [Theory]
    [InlineData("=")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("@")]
    [InlineData("\t")]
    [InlineData("\r")]
    public async Task GetSetsCsv_NeutralizesFormulasInWorkoutAndExerciseNames(string prefix)
    {
        // Arrange
        var exercise = new ExerciseCard { Name = prefix + "Bench", MainMuscle = MuscleGroup.Chest };
        await _factory.SeedAsync(context => context.Exercises.Add(exercise));
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, prefix + "Push", exercise.Id);
        var session = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, session, exercise.Id, new SessionSetRequest(8, 60));

        // Act
        var csv = await client.GetStringAsync("/api/users/me/export/sets");

        // Assert
        var row = csv.TrimStart('﻿')[(CsvHeader.Length + 2)..];
        Assert.Contains(Quote(Neutralized((prefix + "Push").Trim())) + "," + Quote(Neutralized(prefix + "Bench")) + ",1,8,60,", row);
    }

    [Fact]
    public async Task GetSetsCsv_QuotesCommasAndDoubleQuotes_AndLeavesLeadingSpacesAlone()
    {
        // Arrange
        var exercise = new ExerciseCard { Name = " =Bench, \"Flat\"", MainMuscle = MuscleGroup.Chest };
        await _factory.SeedAsync(context => context.Exercises.Add(exercise));
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push, Heavy", exercise.Id);
        var session = await StartSessionAsync(client, workoutId);
        await SaveSetsAsync(client, session, exercise.Id, new SessionSetRequest(8, 60));

        // Act
        var csv = await client.GetStringAsync("/api/users/me/export/sets");

        // Assert
        Assert.Contains("\"Push, Heavy\",\" =Bench, \"\"Flat\"\"\",1,8,60,", csv);
    }

    [Fact]
    public async Task GetSetsCsv_ReturnsOnlyTheHeader_WhenNoSetWasLogged()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var csv = await client.GetStringAsync("/api/users/me/export/sets");

        // Assert
        Assert.Equal(CsvHeader + "\r\n", csv.TrimStart('﻿'));
    }

    private static string Neutralized(string value) => "=+-@\t\r".Contains(value[0]) ? "'" + value : value;

    private static string Quote(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n')
        ? "\"" + value.Replace("\"", "\"\"") + "\""
        : value;

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }

    private async Task<(ExerciseCard Bench, ExerciseCard Squat)> SeedExercisesAsync()
    {
        var bench = new ExerciseCard { Name = "Bench Press", MainMuscle = MuscleGroup.Chest };
        var squat = new ExerciseCard { Name = "Squat", MainMuscle = MuscleGroup.Legs };
        await _factory.SeedAsync(context => context.Exercises.AddRange(bench, squat));

        return (bench, squat);
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, string name, params Guid[] exerciseIds)
    {
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest(name));
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        var workoutId = created?.Id ?? throw new InvalidOperationException("Create workout did not return an id.");
        var exercises = exerciseIds.Select(id => new WorkoutExerciseRequest(id, 3)).ToList();
        var replace = await client.PutAsJsonAsync($"/api/workouts/{workoutId}/exercises", new ReplaceWorkoutExercisesRequest(exercises));
        replace.EnsureSuccessStatusCode();

        return workoutId;
    }

    private static async Task<Guid> StartSessionAsync(HttpClient client, Guid workoutId)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var created = await response.Content.ReadFromJsonAsync<StartSessionResponse>();

        return created?.Id ?? throw new InvalidOperationException("Start session did not return an id.");
    }

    private static Task SaveSetsAsync(HttpClient client, Guid sessionId, Guid exerciseId, params SessionSetRequest[] sets) =>
        SaveSetsAsync(client, sessionId, exerciseId, null, sets);

    private static async Task SaveSetsAsync(HttpClient client, Guid sessionId, Guid exerciseId, ServedTargetRequest? target, params SessionSetRequest[] sets)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/exercises/{exerciseId}/sets",
            new SaveExerciseSetsRequest(sets, target));

        response.EnsureSuccessStatusCode();
    }

    private static Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Proposal(Guid workoutId, Guid exerciseId, object change)
    {
        var arguments = JsonSerializer.SerializeToElement(change)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        arguments["workoutId"] = workoutId;
        arguments["exerciseId"] = exerciseId;
        arguments["rationale"] = "Stalled.";

        return (messages, _) => ScriptedChatClient.ToolResults(messages).Any()
            ? ScriptedChatClient.Reply("Proposed. Tap Apply to confirm.")
            : ScriptedChatClient.CallTool("propose_exercise_change", arguments);
    }

    private static async Task<ChatMessageResponse> SendChatAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("My bench is stuck", null));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ChatMessageResponse>() ?? throw new InvalidOperationException("Empty reply.");
    }
}
