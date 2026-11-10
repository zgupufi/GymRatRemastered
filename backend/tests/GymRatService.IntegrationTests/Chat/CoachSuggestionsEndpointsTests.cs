using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Common.Filtering;
using GymRatService.Repository.Entities;
using Microsoft.Extensions.AI;

namespace GymRatService.IntegrationTests.Chat;

public sealed class CoachSuggestionsEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();
    private readonly ScriptedChatClient _chat = new();

    public CoachSuggestionsEndpointsTests()
    {
        _factory.ChatClient = _chat;
        _factory.Settings["AiCoach:MaximumToolIterations"] = "8";
    }

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("GET", "/api/coach/suggestions")]
    [InlineData("POST", "/api/coach/suggestions/00000000-0000-0000-0000-000000000001/apply")]
    [InlineData("POST", "/api/coach/suggestions/00000000-0000-0000-0000-000000000001/dismiss")]
    public async Task CoachEndpoints_ReturnUnauthorized_WhenTheCallerIsNotAuthenticated(string method, string url)
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
    public async Task Propose_ReturnsACardAndAPendingSuggestion_WithoutChangingTheWorkout()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 3, repsMin = 6, repsMax = 10 }));

        // Act
        var reply = await SendAsync(client);

        // Assert
        var card = Assert.Single(reply.Suggestions);
        Assert.Equal(
            (workoutId, "Push", bench.Id, "Bench Press", (int?)3, (int?)6, (int?)10, (decimal?)null),
            (card.WorkoutId, card.WorkoutName, card.ExerciseId, card.ExerciseName, card.TargetSets, card.RepsMin, card.RepsMax, card.WeightStepKg));
        Assert.Equal((4, 8, 12, 2.5m, "Stalled.", SuggestionStatus.Pending), (card.CurrentTargetSets, card.CurrentRepsMin, card.CurrentRepsMax, card.CurrentWeightStepKg, card.Rationale, card.Status));
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Equal(card.Id, Assert.Single(pending!.Items).Id);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal((4, 8, 12), (workout!.Exercises[0].TargetSets, workout.Exercises[0].RepsMin, workout.Exercises[0].RepsMax));
    }

    [Fact]
    public async Task Apply_ChangesEveryOccurrenceAndRemovesTheSuggestionFromThePendingList()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 3, weightStepKg = 5 }));
        var card = Assert.Single((await SendAsync(client)).Suggestions);

        // Act
        var response = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);
        var again = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertBadRequestAsync(again, ErrorCodes.CoachSuggestionNotPending);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.All(workout!.Exercises, entry => Assert.Equal((3, 8, 12, 5m), (entry.TargetSets, entry.RepsMin, entry.RepsMax, entry.WeightStepKg)));
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Empty(pending!.Items);
    }

    [Fact]
    public async Task Dismiss_LeavesTheWorkoutUnchanged()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 5 }));
        var card = Assert.Single((await SendAsync(client)).Suggestions);

        // Act
        var response = await client.PostAsync($"/api/coach/suggestions/{card.Id}/dismiss", null);
        var apply = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertBadRequestAsync(apply, ErrorCodes.CoachSuggestionNotPending);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal(4, workout!.Exercises[0].TargetSets);
    }

    [Fact]
    public async Task Apply_ReturnsStale_WhenTheWorkoutChangedAfterTheProposal()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 3 }));
        var card = Assert.Single((await SendAsync(client)).Suggestions);
        await client.PutAsJsonAsync(
            $"/api/workouts/{workoutId}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(bench.Id, 4, 10, 15)]));

        // Act
        var response = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.CoachSuggestionStale);
    }

    [Fact]
    public async Task Apply_ReturnsExerciseNotInWorkout_WhenTheExerciseWasRemoved()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 3 }));
        var card = Assert.Single((await SendAsync(client)).Suggestions);
        await client.PutAsJsonAsync($"/api/workouts/{workoutId}/exercises", new ReplaceWorkoutExercisesRequest([]));

        // Act
        var response = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.CoachSuggestionExerciseNotInWorkout);
    }

    [Fact]
    public async Task Suggestions_AreHiddenFromOtherUsers()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var owner = await CreateClientAsync("owner@example.com");
        var intruder = await CreateClientAsync("intruder@example.com");
        var workoutId = await CreateWorkoutAsync(owner, bench.Id, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 3 }));
        var card = Assert.Single((await SendAsync(owner)).Suggestions);

        // Act
        var apply = await intruder.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);
        var dismiss = await intruder.PostAsync($"/api/coach/suggestions/{card.Id}/dismiss", null);
        var list = await intruder.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");

        // Assert
        await AssertBadRequestAsync(apply, ErrorCodes.CoachSuggestionNotFound);
        await AssertBadRequestAsync(dismiss, ErrorCodes.CoachSuggestionNotFound);
        Assert.Empty(list!.Items);
    }

    [Fact]
    public async Task Propose_ReplacesThePendingSuggestionForTheSameExercise()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("propose_exercise_change", new { workoutId, exerciseId = bench.Id, targetSets = 3, rationale = "First." }),
            1 => ScriptedChatClient.CallTool("propose_exercise_change", new { workoutId, exerciseId = bench.Id, targetSets = 5, rationale = "Second." }),
            _ => ScriptedChatClient.Reply("Two ideas."),
        });

        // Act
        var reply = await SendAsync(client);

        // Assert
        Assert.Equal("Second.", Assert.Single(reply.Suggestions).Rationale);
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Equal("Second.", Assert.Single(pending!.Items).Rationale);
    }

    [Fact]
    public async Task Propose_AllowsAtMostThreeProposalsPerReply()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutIds = new List<Guid>();
        for (var index = 0; index < 4; index++)
        {
            workoutIds.Add(await CreateWorkoutAsync(client, bench.Id, 4));
        }

        Script((messages, _) => ToolCount(messages) < 4
            ? ScriptedChatClient.CallTool("propose_exercise_change", new { workoutId = workoutIds[ToolCount(messages)], exerciseId = bench.Id, targetSets = 3, rationale = "Less." })
            : ScriptedChatClient.Reply("Done."));

        // Act
        var reply = await SendAsync(client);

        // Assert
        Assert.Equal(3, reply.Suggestions.Count);
        var last = ScriptedChatClient.LastToolResult(_chat.Calls[^1]);
        Assert.Equal(ErrorCodes.CoachTooManyProposals, last.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Propose_ReturnsToolErrorsAndNoCards_ForInvalidProposals()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var other = await SeedExerciseAsync("Row");
        var client = await CreateClientAsync("ana@example.com");
        var foreign = await CreateClientAsync("other@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        var foreignWorkoutId = await CreateWorkoutAsync(foreign, bench.Id, 4);
        object[] proposals =
        [
            new { workoutId = foreignWorkoutId, exerciseId = bench.Id, targetSets = 3, rationale = "x" },
            new { workoutId, exerciseId = other.Id, targetSets = 3, rationale = "x" },
            new { workoutId, exerciseId = bench.Id, rationale = "x" },
            new { workoutId, exerciseId = bench.Id, targetSets = 6, rationale = "x" },
            new { workoutId, exerciseId = bench.Id, repsMin = 14, rationale = "x" },
            new { workoutId, exerciseId = bench.Id, targetSets = "three", rationale = "x" },
            new { workoutId, exerciseId = bench.Id, targetSets = 3 },
        ];
        Script((messages, _) => ToolCount(messages) < proposals.Length
            ? ScriptedChatClient.CallTool("propose_exercise_change", proposals[ToolCount(messages)])
            : ScriptedChatClient.Reply("Nothing to change."));

        // Act
        var reply = await SendAsync(client);

        // Assert
        Assert.Empty(reply.Suggestions);
        Assert.Equal("Nothing to change.", reply.Reply);
        var results = ScriptedChatClient.ToolResults(_chat.Calls[^1]).Select(result => JsonSerializer.Serialize(result)).ToList();
        Assert.Equal(proposals.Length, results.Count);
        Assert.Contains(ErrorCodes.WorkoutNotFound, results[0]);
        Assert.Contains(ErrorCodes.CoachSuggestionExerciseNotInWorkout, results[1]);
        Assert.Contains("error", results[2]);
        Assert.Contains(ErrorCodes.CoachSuggestionInvalidChange, results[3]);
        Assert.Contains(ErrorCodes.CoachSuggestionInvalidChange, results[4]);
        Assert.DoesNotContain("proposed", results[5]);
        Assert.DoesNotContain("proposed", results[6]);
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Empty(pending!.Items);
    }

    [Fact]
    public async Task HistoryAndTargetTools_ReturnTheLoggedEffortAndTodaysTarget()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 2);
        var session = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var sessionId = (await session.Content.ReadFromJsonAsync<StartSessionResponse>())!.Id;
        await client.PutAsJsonAsync($"/api/sessions/{sessionId}/exercises/{bench.Id}/sets", new SaveExerciseSetsRequest([new SessionSetRequest(8, 80, 0)]));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);
        Script((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("get_exercise_history", new { exerciseId = bench.Id }),
            1 => ScriptedChatClient.CallTool("get_targets", new { workoutId }),
            _ => ScriptedChatClient.Reply("Hold at 80."),
        });

        // Act
        await SendAsync(client);

        // Assert
        var results = ScriptedChatClient.ToolResultElements(_chat.Calls[^1]);
        var set = results[0][0].GetProperty("sets")[0];
        Assert.Equal((8, 80m, 0), (set.GetProperty("reps").GetInt32(), set.GetProperty("weightKg").GetDecimal(), set.GetProperty("repsInReserve").GetInt32()));
        Assert.Equal(nameof(ProgressionReason.Hold), results[1][0].GetProperty("reason").GetString());
    }

    private static int ToolCount(IReadOnlyList<ChatMessage> messages) => ScriptedChatClient.ToolResults(messages).Count();

    private static Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Proposal(Guid workoutId, Guid exerciseId, object change)
    {
        var arguments = JsonSerializer.SerializeToElement(change)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        arguments["workoutId"] = workoutId;
        arguments["exerciseId"] = exerciseId;
        arguments["rationale"] = "Stalled.";

        return (messages, _) => ToolCount(messages) == 0
            ? ScriptedChatClient.CallTool("propose_exercise_change", arguments)
            : ScriptedChatClient.Reply("Proposed. Tap Apply to confirm.");
    }

    private void Script(Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> script) => _chat.Script = script;

    private static async Task<ChatMessageResponse> SendAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("My bench is stuck", null));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ChatMessageResponse>() ?? throw new InvalidOperationException("Empty reply.");
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(code, error?.Code);
    }

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        await _factory.SeedAsync(_ => { });

        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest(email, "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }

    private async Task<ExerciseCard> SeedExerciseAsync(string name = "Bench Press")
    {
        var exercise = new ExerciseCard { Name = name, MainMuscle = "Chest", Equipment = "Barbell" };
        await _factory.SeedAsync(context => context.Exercises.Add(exercise));

        return exercise;
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client, Guid exerciseId, params int[] sets)
    {
        var response = await client.PostAsJsonAsync("/api/workouts", new CreateWorkoutRequest("Push"));
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        var id = created?.Id ?? throw new InvalidOperationException("Create workout did not return an id.");

        var replace = await client.PutAsJsonAsync(
            $"/api/workouts/{id}/exercises",
            new ReplaceWorkoutExercisesRequest([.. sets.Select(count => new WorkoutExerciseRequest(exerciseId, count))]));
        replace.EnsureSuccessStatusCode();

        return id;
    }
}
