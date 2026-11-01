using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.DTOs.Split;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using Microsoft.Extensions.AI;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using ChatRole = GymRatService.Common.Enums.ChatRole;

namespace GymRatService.IntegrationTests.Chat;

public sealed class ChatEndpointsTests : IDisposable
{
    private readonly GymRatApiFactory _factory = new();
    private readonly ScriptedChatClient _chat = new();

    public ChatEndpointsTests()
    {
        _factory.ChatClient = _chat;
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SendMessage_ReturnsUnauthorized_WhenTheCallerIsNotAuthenticated()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_ReturnsTheReplyAndSendsTheLastTenHistoryMessagesWithTheConfiguredModel()
    {
        // Arrange
        var chat = UseChatClient((_, _) => ScriptedChatClient.Reply("Train hard."));
        _factory.Settings["AiCoach:Model"] = "cheap-model";
        _factory.Settings["AiCoach:MaximumOutputTokens"] = "321";
        var client = await CreateClientAsync("ana@example.com");
        var history = Enumerable.Range(1, 12)
            .Select(index => new ChatHistoryItem(index % 2 == 1 ? ChatRole.User : ChatRole.Assistant, $"message {index}"))
            .ToList();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("How do I get stronger?", history));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal(new ChatMessageResponse("Train hard.", false), body);
        var sent = Assert.Single(chat.Calls);
        Assert.Equal(AiChatRole.System, sent[0].Role);
        Assert.Equal(12, sent.Count);
        Assert.Equal("message 3", sent[1].Text);
        Assert.Equal(AiChatRole.User, sent[1].Role);
        Assert.Equal(AiChatRole.Assistant, sent[2].Role);
        Assert.Equal("How do I get stronger?", sent[^1].Text);
        Assert.Equal(AiChatRole.User, sent[^1].Role);
        Assert.Equal("cheap-model", chat.Options[0]?.ModelId);
        Assert.Equal(321, chat.Options[0]?.MaxOutputTokens);
    }

    [Fact]
    public async Task SendMessage_AcceptsAMessageWithoutHistoryAndUsesTheDefaultModel()
    {
        // Arrange
        var chat = UseChatClient((_, _) => ScriptedChatClient.Reply("Hello"));
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal("Hello", body?.Reply);
        Assert.Equal(2, chat.Calls[0].Count);
        Assert.Equal("gemini-2.5-flash-lite", chat.Options[0]?.ModelId);
    }

    [Fact]
    public async Task SendMessage_AcceptsHistoryRolesAsStrings()
    {
        // Arrange
        var chat = UseChatClient((_, _) => ScriptedChatClient.Reply("ok"));
        var client = await CreateClientAsync("ana@example.com");
        var content = new StringContent(
            """{"message":"Hi","history":[{"role":"assistant","content":"Welcome"}]}""",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/chat/messages", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AiChatRole.Assistant, chat.Calls[0][1].Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_ReturnsInvalidRequest_WhenTheMessageIsBlank(string message)
    {
        // Arrange
        var chat = UseChatClient((_, _) => ScriptedChatClient.Reply("unused"));
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest(message, null));

        // Assert
        var error = await AssertBadRequestAsync(response, ErrorCodes.InvalidRequest);
        Assert.Equal("Message", Assert.Single(error.Errors!).Property);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task SendMessage_ReturnsInvalidRequest_WhenTheMessageExceedsTheLimit()
    {
        // Arrange
        UseChatClient((_, _) => ScriptedChatClient.Reply("unused"));
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var accepted = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest(new string('a', 1000), null));
        var rejected = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest(new string('a', 1001), null));

        // Assert
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await AssertBadRequestAsync(rejected, ErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task SendMessage_ReturnsInvalidRequest_WhenAHistoryItemIsEmpty()
    {
        // Arrange
        UseChatClient((_, _) => ScriptedChatClient.Reply("unused"));
        var client = await CreateClientAsync("ana@example.com");
        var history = new[] { new ChatHistoryItem(ChatRole.User, "") };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", history));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task SendMessage_ReturnsInvalidRequest_WhenAHistoryRoleIsUnknown()
    {
        // Arrange
        UseChatClient((_, _) => ScriptedChatClient.Reply("unused"));
        var client = await CreateClientAsync("ana@example.com");
        var content = new StringContent(
            """{"message":"Hi","history":[{"role":7,"content":"x"}]}""",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/chat/messages", content);

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.InvalidRequest);
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenNoProviderIsConfigured()
    {
        // Arrange
        _factory.ChatClient = null;
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.ChatProviderUnavailable);
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenTheConfiguredProviderCannotBeReached()
    {
        // Arrange
        _factory.ChatClient = null;
        _factory.Settings["AiCoach:ApiKey"] = "test-key";
        _factory.Settings["AiCoach:Endpoint"] = "http://127.0.0.1:1/";
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.ChatProviderUnavailable);
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenTheProviderFails()
    {
        // Arrange
        UseChatClient((_, _) => throw new HttpRequestException("boom"));
        var client = await CreateClientAsync("ana@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertBadRequestAsync(response, ErrorCodes.ChatProviderUnavailable);
    }

    [Fact]
    public async Task SendMessage_ReturnsRateLimited_WhenTheUserSendsTooManyMessagesInTheWindow()
    {
        // Arrange
        UseChatClient((_, _) => ScriptedChatClient.Reply("ok"));
        _factory.Settings["AiCoach:RateLimitRequests"] = "2";
        _factory.Settings["AiCoach:RateLimitWindowSeconds"] = "60";
        var ana = await CreateClientAsync("ana@example.com");
        var bob = await CreateClientAsync("bob@example.com");
        var message = new ChatMessageRequest("Hi", null);

        // Act
        var first = await ana.PostAsJsonAsync("/api/chat/messages", message);
        var second = await ana.PostAsJsonAsync("/api/chat/messages", message);
        var third = await ana.PostAsJsonAsync("/api/chat/messages", message);
        var otherUser = await bob.PostAsJsonAsync("/api/chat/messages", message);
        _factory.Clock.Advance(TimeSpan.FromSeconds(61));
        var afterWindow = await ana.PostAsJsonAsync("/api/chat/messages", message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AssertBadRequestAsync(third, ErrorCodes.ChatRateLimited);
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterWindow.StatusCode);
    }

    [Fact]
    public async Task SendMessage_ReturnsTokenBudgetExceeded_UntilTheNextDay()
    {
        // Arrange
        UseChatClient((_, _) => ScriptedChatClient.Reply("ok", totalTokens: 100));
        _factory.Settings["AiCoach:DailyTokenBudget"] = "150";
        var client = await CreateClientAsync("ana@example.com");
        var message = new ChatMessageRequest("Hi", null);

        // Act
        var first = await client.PostAsJsonAsync("/api/chat/messages", message);
        var second = await client.PostAsJsonAsync("/api/chat/messages", message);
        var third = await client.PostAsJsonAsync("/api/chat/messages", message);
        _factory.Clock.Advance(TimeSpan.FromDays(1));
        var nextDay = await client.PostAsJsonAsync("/api/chat/messages", message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AssertBadRequestAsync(third, ErrorCodes.ChatTokenBudgetExceeded);
        Assert.Equal(HttpStatusCode.OK, nextDay.StatusCode);
    }

    [Fact]
    public async Task SendMessage_CountsNothingTowardsTheBudget_WhenTheProviderReportsNoUsage()
    {
        // Arrange
        UseChatClient((_, _) => new ChatResponse(new ChatMessage(AiChatRole.Assistant, "ok")));
        _factory.Settings["AiCoach:DailyTokenBudget"] = "1";
        var client = await CreateClientAsync("ana@example.com");
        var message = new ChatMessageRequest("Hi", null);

        // Act
        var first = await client.PostAsJsonAsync("/api/chat/messages", message);
        var second = await client.PostAsJsonAsync("/api/chat/messages", message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task SendMessage_CreatesAndReadsWorkoutsThroughTools()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var chat = UseChatClient((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("create_workout", new
            {
                name = "Push",
                exercises = new[] { new { exerciseId = bench.Id, targetSets = 4 }, new { exerciseId = squat.Id, targetSets = 3 } },
            }),
            1 => ScriptedChatClient.CallTool("list_workouts"),
            _ => ScriptedChatClient.Reply("Created Push."),
        });

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Make me a push workout", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal(new ChatMessageResponse("Created Push.", true), body);
        var listed = ScriptedChatClient.LastToolResult(chat.Calls[^1]);
        Assert.Equal("Push", listed[0].GetProperty("name").GetString());
        Assert.Equal(2, listed[0].GetProperty("exerciseCount").GetInt32());
        var workouts = await client.GetFromJsonAsync<JsonElement>("/api/workouts");
        var workoutId = workouts.GetProperty("items")[0].GetProperty("id").GetGuid();
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal([bench.Id, squat.Id], workout!.Exercises.Select(entry => entry.Exercise.Id));
        Assert.Equal([4, 3], workout.Exercises.Select(entry => entry.TargetSets));
    }

    [Fact]
    public async Task SendMessage_CreatesAnEmptyWorkoutWhenNoExercisesAreGiven()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        UseChatClient((messages, _) => ToolCount(messages) == 0
            ? ScriptedChatClient.CallTool("create_workout", new { name = "Empty" })
            : ScriptedChatClient.Reply("Done."));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Make an empty workout", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.True(body?.WorkoutsChanged);
        var workouts = await client.GetFromJsonAsync<JsonElement>("/api/workouts");
        Assert.Equal("Empty", workouts.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal(0, workouts.GetProperty("items")[0].GetProperty("exerciseCount").GetInt32());
    }

    [Fact]
    public async Task SendMessage_GetsUpdatesAndDeletesWorkoutsThroughTools()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push", (bench.Id, 3));
        var chat = UseChatClient((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("get_workout", new { workoutId }),
            1 => ScriptedChatClient.CallTool("update_workout", new { workoutId, name = "Legs", exercises = new[] { new { exerciseId = squat.Id, targetSets = 5 } } }),
            2 => ScriptedChatClient.CallTool("get_workout", new { workoutId }),
            3 => ScriptedChatClient.CallTool("delete_workout", new { workoutId }),
            _ => ScriptedChatClient.Reply("Done."),
        });

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Rework my workout then delete it", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.True(body?.WorkoutsChanged);
        var results = ScriptedChatClient.ToolResultElements(chat.Calls[^1]);
        Assert.Equal("Push", results[0].GetProperty("name").GetString());
        Assert.True(results[1].GetProperty("updated").GetBoolean());
        Assert.Equal("Legs", results[2].GetProperty("name").GetString());
        Assert.Equal(5, results[2].GetProperty("exercises")[0].GetProperty("targetSets").GetInt32());
        Assert.True(results[3].GetProperty("deleted").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/workouts/{workoutId}")).StatusCode);
    }

    [Fact]
    public async Task SendMessage_UpdatesOnlyTheNameOrOnlyTheExercisesWhenOnlyOneIsGiven()
    {
        // Arrange
        var (bench, squat) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push", (bench.Id, 3));
        UseChatClient((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("update_workout", new { workoutId, exercises = new[] { new { exerciseId = squat.Id, targetSets = 2 } } }),
            1 => ScriptedChatClient.CallTool("update_workout", new { workoutId, name = "Renamed" }),
            _ => ScriptedChatClient.Reply("Done."),
        });

        // Act
        await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Tweak it", null));

        // Assert
        var workout = await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}");
        Assert.Equal("Renamed", workout!.Name);
        Assert.Equal([squat.Id], workout.Exercises.Select(entry => entry.Exercise.Id));
    }

    [Fact]
    public async Task SendMessage_ReportsNoChange_WhenTheUpdateToolHasNothingToChange()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push", (bench.Id, 3));
        UseChatClient((messages, _) => ToolCount(messages) == 0
            ? ScriptedChatClient.CallTool("update_workout", new { workoutId })
            : ScriptedChatClient.Reply("Nothing to do."));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Update it", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.False(body?.WorkoutsChanged);
    }

    [Fact]
    public async Task SendMessage_ReadsTheSplitAndSearchesExercisesThroughTools()
    {
        // Arrange
        var (bench, _) = await SeedExercisesAsync();
        var client = await CreateClientAsync("ana@example.com");
        var workoutId = await CreateWorkoutAsync(client, "Push", (bench.Id, 3));
        await client.PutAsJsonAsync("/api/split/2", new AssignSplitDayRequest(workoutId));
        var chat = UseChatClient((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("read_split"),
            1 => ScriptedChatClient.CallTool("search_exercises", new { term = "squat" }),
            2 => ScriptedChatClient.CallTool("search_exercises", new { muscle = "Chest" }),
            3 => ScriptedChatClient.CallTool("search_exercises"),
            _ => ScriptedChatClient.Reply("Done."),
        });

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("What is my week?", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.False(body?.WorkoutsChanged);
        var results = ScriptedChatClient.ToolResultElements(chat.Calls[^1]);
        Assert.Equal(7, results[0].GetArrayLength());
        Assert.Equal("Push", results[0][1].GetProperty("workout").GetProperty("name").GetString());
        Assert.Equal(["Squat"], results[1].EnumerateArray().Select(entry => entry.GetProperty("name").GetString()));
        Assert.Equal(["Bench Press"], results[2].EnumerateArray().Select(entry => entry.GetProperty("name").GetString()));
        Assert.Equal(2, results[3].GetArrayLength());
    }

    [Fact]
    public async Task SendMessage_ReturnsToolErrorsToTheModelInsteadOfFailing()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var client = await CreateClientAsync("ana@example.com");
        var missing = Guid.CreateVersion7();
        var chat = UseChatClient((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("get_workout", new { workoutId = missing }),
            1 => ScriptedChatClient.CallTool("create_workout", new { name = "" }),
            _ => ScriptedChatClient.Reply("That failed."),
        });

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Show me workout", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal(new ChatMessageResponse("That failed.", false), body);
        var results = ScriptedChatClient.ToolResultElements(chat.Calls[^1]);
        Assert.Equal(ErrorCodes.WorkoutNotFound, results[0].GetProperty("error").GetString());
        Assert.Contains("Name", results[1].GetProperty("error")[0].GetString());
    }

    [Fact]
    public async Task SendMessage_ToolsOnlyReachTheCallersOwnWorkouts()
    {
        // Arrange
        await _factory.SeedAsync(_ => { });
        var ana = await CreateClientAsync("ana@example.com");
        var bob = await CreateClientAsync("bob@example.com");
        var workoutId = await CreateWorkoutAsync(ana, "Push");
        var chat = UseChatClient((messages, _) => ToolCount(messages) switch
        {
            0 => ScriptedChatClient.CallTool("delete_workout", new { workoutId }),
            1 => ScriptedChatClient.CallTool("list_workouts"),
            _ => ScriptedChatClient.Reply("Done."),
        });

        // Act
        var response = await bob.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Delete workout", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.False(body?.WorkoutsChanged);
        var results = ScriptedChatClient.ToolResultElements(chat.Calls[^1]);
        Assert.Equal(ErrorCodes.WorkoutNotFound, results[0].GetProperty("error").GetString());
        Assert.Equal(0, results[1].GetArrayLength());
        Assert.Equal("Push", (await ana.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workoutId}"))!.Name);
    }

    private static int ToolCount(IReadOnlyList<ChatMessage> messages) => ScriptedChatClient.ToolResults(messages).Count();

    private ScriptedChatClient UseChatClient(Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> script)
    {
        _chat.Script = script;

        return _chat;
    }

    private static async Task<ErrorResponse> AssertBadRequestAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(code, error?.Code);

        return error!;
    }

    private async Task<HttpClient> CreateClientAsync(string email)
    {
        await _factory.SeedAsync(_ => { });

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
