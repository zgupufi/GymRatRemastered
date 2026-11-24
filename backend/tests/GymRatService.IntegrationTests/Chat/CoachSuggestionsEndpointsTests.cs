using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.DTOs.Coach;
using GymRatService.Common.DTOs.Export;
using GymRatService.Common.DTOs.Split;
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
        _factory.Settings["AiCoach:RateLimitRequests"] = "20";
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
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        await _factory.SeedAsync(context => context.WorkoutExercises.Add(new WorkoutExercise { WorkoutId = workoutId, ExerciseId = bench.Id, Position = 2, TargetSets = 4 }));
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
    public async Task Propose_CountsRejectedProposalsTowardTheLimitOfThreePerReply()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script((messages, _) => ToolCount(messages) switch
        {
            < 3 => ScriptedChatClient.CallTool("propose_exercise_change", new { workoutId, exerciseId = bench.Id, targetSets = 9, rationale = "Too much." }),
            3 => ScriptedChatClient.CallTool("propose_exercise_change", new { workoutId, exerciseId = bench.Id, targetSets = 3, rationale = "Valid." }),
            _ => ScriptedChatClient.Reply("Done."),
        });

        // Act
        var reply = await SendAsync(client);

        // Assert
        Assert.Empty(reply.Suggestions);
        var last = ScriptedChatClient.LastToolResult(_chat.Calls[^1]);
        Assert.Equal(ErrorCodes.CoachTooManyProposals, last.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Targets_SerializeReasonsAsStrings()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Proposal(workoutId, bench.Id, new { targetSets = 3 }));
        await SendAsync(client);

        // Act
        var targets = await client.GetStringAsync($"/api/workouts/{workoutId}/targets");
        var suggestions = await client.GetStringAsync("/api/coach/suggestions");
        var served = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var sessionId = (await served.Content.ReadFromJsonAsync<StartSessionResponse>())!.Id;
        var save = await client.PutAsync(
            $"/api/sessions/{sessionId}/exercises/{bench.Id}/sets",
            JsonContent.Create(JsonSerializer.Deserialize<JsonElement>("""{"sets":[{"reps":8,"weightKg":60}],"target":{"weightKg":null,"reps":8,"reason":"NoHistory"}}""")));

        // Assert
        Assert.Contains("\"reason\":\"NoHistory\"", targets);
        Assert.Contains("\"status\":\"Pending\"", suggestions);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
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
        var results = new List<string>();
        var cards = 0;

        // Act
        foreach (var proposal in proposals)
        {
            Script((messages, _) => ToolCount(messages) == 0
                ? ScriptedChatClient.CallTool("propose_exercise_change", proposal)
                : ScriptedChatClient.Reply("Nothing to change."));
            var reply = await SendAsync(client);
            cards += reply.Suggestions.Count;
            results.Add(JsonSerializer.Serialize(ScriptedChatClient.ToolResults(_chat.Calls[^1]).Single()));
        }

        // Assert
        Assert.Equal(0, cards);
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

    [Fact]
    public async Task ProposeReplacement_ShowsTheChangesOnTheCard_AndApplyReplacesTheExercises()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var row = await SeedExerciseAsync("Row");
        var squat = await SeedExerciseAsync("Squat");
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        await client.PutAsJsonAsync(
            $"/api/workouts/{workoutId}/exercises",
            new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(bench.Id, 4), new WorkoutExerciseRequest(squat.Id, 3)]));
        Script(Replacement(workoutId, new[] { new { exerciseId = row.Id, targetSets = 3 }, new { exerciseId = bench.Id, targetSets = 2 } }));

        // Act
        var card = Assert.Single((await SendAsync(client)).Suggestions);
        var workoutBefore = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        var apply = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        Assert.Equal((SuggestionKind.ReplaceExercises, workoutId, "Push", (Guid?)null), (card.Kind, card.WorkoutId, card.WorkoutName, card.ExerciseId));
        Assert.Equal(
            [(bench.Id, 2, (int?)4, SuggestionExerciseChange.Kept), (squat.Id, 3, (int?)null, SuggestionExerciseChange.Removed), (row.Id, 3, (int?)null, SuggestionExerciseChange.Added)],
            card.Exercises.Select(entry => (entry.ExerciseId, entry.TargetSets, entry.PreviousTargetSets, entry.Change)).OrderBy(entry => entry.Change));
        Assert.Equal([bench.Id, squat.Id], workoutBefore!.Exercises.Select(entry => entry.Exercise.Id));
        Assert.Equal(HttpStatusCode.NoContent, apply.StatusCode);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal([(row.Id, 3), (bench.Id, 2)], workout!.Exercises.Select(entry => (entry.Exercise.Id, entry.TargetSets)));
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Empty(pending!.Items);
        await AssertBadRequestAsync(await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null), ErrorCodes.CoachSuggestionNotPending);
    }

    [Fact]
    public async Task ProposeDeletion_ReturnsTheScheduledDaysAndSessionCount_AndApplyDeletesTheWorkout()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 3);
        await client.PutAsJsonAsync("/api/split/3", new AssignSplitDayRequest(workoutId));
        await client.PutAsJsonAsync("/api/split/5", new AssignSplitDayRequest(workoutId));
        var session = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(workoutId));
        var sessionId = (await session.Content.ReadFromJsonAsync<StartSessionResponse>())!.Id;
        await client.PutAsJsonAsync($"/api/sessions/{sessionId}/exercises/{bench.Id}/sets", new SaveExerciseSetsRequest([new SessionSetRequest(8, 60, 2)]));
        await client.PostAsync($"/api/sessions/{sessionId}/finish", null);
        Script(Deletion(workoutId));

        // Act
        var card = Assert.Single((await SendAsync(client)).Suggestions);
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        var apply = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        Assert.Equal((SuggestionKind.DeleteWorkout, "Push", 1), (card.Kind, card.WorkoutName, card.SessionCount));
        Assert.Equal([3, 5], card.ScheduledDays.Order());
        Assert.Equal([(bench.Id, 3, SuggestionExerciseChange.Kept)], card.Exercises.Select(entry => (entry.ExerciseId, entry.TargetSets, entry.Change)));
        var listed = Assert.Single(pending!.Items);
        Assert.Equal([3, 5], listed.ScheduledDays.Order());
        Assert.Equal(1, listed.SessionCount);
        Assert.Equal(HttpStatusCode.NoContent, apply.StatusCode);
        await AssertBadRequestAsync(await client.GetAsync($"/api/workouts/{workoutId}"), ErrorCodes.WorkoutNotFound);
        var after = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Empty(after!.Items);
    }

    [Theory]
    [InlineData(SuggestionKind.ReplaceExercises)]
    [InlineData(SuggestionKind.DeleteWorkout)]
    public async Task Apply_ReturnsStaleAndExpiresTheSuggestion_WhenTheWorkoutChangedAfterAWorkoutProposal(SuggestionKind kind)
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var row = await SeedExerciseAsync("Row");
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(kind == SuggestionKind.ReplaceExercises
            ? Replacement(workoutId, new[] { new { exerciseId = row.Id, targetSets = 3 } })
            : Deletion(workoutId));
        var card = Assert.Single((await SendAsync(client)).Suggestions);
        await client.PutAsJsonAsync($"/api/workouts/{workoutId}/exercises", new ReplaceWorkoutExercisesRequest([new WorkoutExerciseRequest(bench.Id, 5)]));

        // Act
        var response = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);
        var again = await client.PostAsync($"/api/coach/suggestions/{card.Id}/apply", null);

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.CoachSuggestionStale);
        await AssertBadRequestAsync(again, ErrorCodes.CoachSuggestionNotPending);
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal([(bench.Id, 5)], workout!.Exercises.Select(entry => (entry.Exercise.Id, entry.TargetSets)));
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Empty(pending!.Items);
    }

    [Fact]
    public async Task ProposeWorkoutChanges_ReturnToolErrorsAndNoCards_ForInvalidProposals()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var client = await CreateClientAsync("ana@example.com");
        var foreign = await CreateClientAsync("other@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        var foreignWorkoutId = await CreateWorkoutAsync(foreign, bench.Id, 4);
        var unknownExercise = Guid.CreateVersion7();
        (string Tool, object Arguments)[] proposals =
        [
            ("propose_workout_replacement", new { workoutId = foreignWorkoutId, exercises = new[] { new { exerciseId = bench.Id, targetSets = 3 } }, rationale = "x" }),
            ("propose_workout_replacement", new { workoutId, exercises = new[] { new { exerciseId = unknownExercise, targetSets = 3 } }, rationale = "x" }),
            ("propose_workout_replacement", new { workoutId, exercises = new[] { new { exerciseId = bench.Id, targetSets = 4 } }, rationale = "x" }),
            ("propose_workout_replacement", new { workoutId, exercises = new[] { new { exerciseId = bench.Id, targetSets = 3 }, new { exerciseId = bench.Id, targetSets = 3 } }, rationale = "x" }),
            ("propose_workout_replacement", new { workoutId, exercises = Enumerable.Range(0, 51).Select(_ => new { exerciseId = Guid.CreateVersion7(), targetSets = 3 }).ToArray(), rationale = "x" }),
            ("propose_workout_replacement", new { workoutId, exercises = new[] { new { exerciseId = bench.Id, targetSets = 3 } } }),
            ("propose_workout_deletion", new { workoutId = foreignWorkoutId, rationale = "x" }),
            ("propose_workout_deletion", new { workoutId }),
        ];
        var results = new List<string>();
        var cards = 0;

        // Act
        foreach (var (tool, arguments) in proposals)
        {
            Script((messages, _) => ToolCount(messages) == 0
                ? ScriptedChatClient.CallTool(tool, arguments)
                : ScriptedChatClient.Reply("Nothing to change."));
            var reply = await SendAsync(client);
            cards += reply.Suggestions.Count;
            results.Add(JsonSerializer.Serialize(ScriptedChatClient.ToolResults(_chat.Calls[^1]).Single()));
        }

        // Assert
        Assert.Equal(0, cards);
        Assert.Contains(ErrorCodes.WorkoutNotFound, results[0]);
        Assert.Contains(ErrorCodes.WorkoutExerciseNotFound, results[1]);
        Assert.Contains(ErrorCodes.CoachSuggestionInvalidChange, results[2]);
        Assert.Contains("error", results[3]);
        Assert.Contains("error", results[4]);
        Assert.DoesNotContain("proposed", results[5]);
        Assert.Contains(ErrorCodes.WorkoutNotFound, results[6]);
        Assert.DoesNotContain("proposed", results[7]);
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");
        Assert.Empty(pending!.Items);
    }

    [Fact]
    public async Task ProposeWorkoutChanges_ReplaceThePendingProposalOfTheSameKindForTheSameWorkout()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var row = await SeedExerciseAsync("Row");
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Replacement(workoutId, new[] { new { exerciseId = row.Id, targetSets = 3 } }, "First."));
        await SendAsync(client);
        Script(Replacement(workoutId, new[] { new { exerciseId = row.Id, targetSets = 5 } }, "Second."));
        await SendAsync(client);
        Script(Deletion(workoutId, "Drop it."));
        await SendAsync(client);

        // Act
        var pending = await client.GetFromJsonAsync<PagedResult<CoachSuggestionDto>>("/api/coach/suggestions");

        // Assert
        Assert.Equal(
            [(SuggestionKind.ReplaceExercises, "Second."), (SuggestionKind.DeleteWorkout, "Drop it.")],
            pending!.Items.Select(entry => (entry.Kind, entry.Rationale)).OrderBy(entry => entry.Kind));
    }

    [Fact]
    public async Task Export_IncludesTheKindAndSnapshotFieldsOfWorkoutProposals()
    {
        // Arrange
        var bench = await SeedExerciseAsync();
        var row = await SeedExerciseAsync("Row");
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, bench.Id, 4);
        Script(Replacement(workoutId, new[] { new { exerciseId = row.Id, targetSets = 3 } }));
        await SendAsync(client);
        Script(Deletion(workoutId));
        await SendAsync(client);

        // Act
        var export = await client.GetFromJsonAsync<UserExportDto>("/api/users/me/export");

        // Assert
        Assert.Equal(
            [SuggestionKind.ReplaceExercises, SuggestionKind.DeleteWorkout],
            export!.CoachSuggestions.Select(entry => entry.Kind).Order());
        Assert.All(export.CoachSuggestions, entry => Assert.Equal(("Push", (Guid?)null, string.Empty, SuggestionStatus.Pending), (entry.WorkoutName, entry.ExerciseId, entry.ExerciseName, entry.Status)));
    }

    private static Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Replacement(Guid workoutId, object exercises, string rationale = "Better split.") =>
        (messages, _) => ToolCount(messages) == 0
            ? ScriptedChatClient.CallTool("propose_workout_replacement", new { workoutId, exercises, rationale })
            : ScriptedChatClient.Reply("Proposed. Tap Apply to confirm.");

    private static Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Deletion(Guid workoutId, string rationale = "Never used.") =>
        (messages, _) => ToolCount(messages) == 0
            ? ScriptedChatClient.CallTool("propose_workout_deletion", new { workoutId, rationale })
            : ScriptedChatClient.Reply("Proposed. Tap Apply to confirm.");

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
        var exercise = new ExerciseCard { Name = name, MainMuscle = MuscleGroup.Chest, Equipment = Equipment.Barbell };
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
