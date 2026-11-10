using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;

namespace GymRatService.IntegrationTests.Chat;

public sealed class ChatOpenRouterEndpointsTests : IDisposable
{
    private const string Primary = "primary/model:free";

    private const string Fallback = "fallback/model:free";

    private readonly GymRatApiFactory _factory = new();
    private FakeOpenRouterServer? _server;

    public void Dispose()
    {
        _server?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task SendMessage_SendsAnAuthenticatedChatCompletionRequestAndReturnsTheReply()
    {
        // Arrange
        var client = await StartAsync(_ => FakeOpenRouterReply.Text("Lift heavy."));
        var history = new[] { new ChatHistoryItem(ChatRole.User, "Earlier"), new ChatHistoryItem(ChatRole.Assistant, "Earlier answer") };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("How do I get stronger?", history));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(new ChatMessageResponse("Lift heavy.", false, []), await response.Content.ReadFromJsonAsync<ChatMessageResponse>());
        var request = Assert.Single(_server!.Requests);
        Assert.Equal("/api/v1/chat/completions", request.Path);
        Assert.Equal("Bearer test-key", request.Authorization);
        Assert.Equal(Primary, request.Model);
        Assert.False(request.Body.TryGetProperty("response_format", out _));
        var messages = request.Body.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(["system", "user", "assistant", "user"], messages.Select(message => message.GetProperty("role").GetString()));
        Assert.Equal("How do I get stronger?", messages[^1].GetProperty("content").GetString());
        var tools = request.Body.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(
            ["create_workout", "delete_workout", "get_exercise_history", "get_targets", "get_workout", "list_workouts", "propose_exercise_change", "read_split", "search_exercises", "update_workout"],
            tools.Order());
    }

    [Fact]
    public async Task SendMessage_RunsAToolRequestedByTheModelAndSendsTheResultBack()
    {
        // Arrange
        var client = await StartAsync(request => request.Body.GetProperty("messages").EnumerateArray().Any(message => message.GetProperty("role").GetString() == "tool")
            ? FakeOpenRouterReply.Text("You have one workout.")
            : FakeOpenRouterReply.ToolCall("list_workouts"));
        await client.PostAsJsonAsync("/api/workouts", new GymRatService.Common.DTOs.Workouts.CreateWorkoutRequest("Push"));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("What workouts do I have?", null));

        // Assert
        Assert.Equivalent(new ChatMessageResponse("You have one workout.", false, []), await response.Content.ReadFromJsonAsync<ChatMessageResponse>());
        Assert.Equal(2, _server!.Requests.Count);
        var toolMessage = _server.Requests[1].Body.GetProperty("messages").EnumerateArray().Single(message => message.GetProperty("role").GetString() == "tool");
        Assert.Contains("Push", toolMessage.GetProperty("content").ToString());
    }

    [Fact]
    public async Task SendMessage_CreatesAWorkoutWhenTheModelCallsTheCreateTool()
    {
        // Arrange
        var client = await StartAsync(request => request.Body.GetProperty("messages").EnumerateArray().Any(message => message.GetProperty("role").GetString() == "tool")
            ? FakeOpenRouterReply.Text("Created.")
            : FakeOpenRouterReply.ToolCall("create_workout", """{"name":"Pull day"}"""));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Create a pull day", null));

        // Assert
        Assert.Equivalent(new ChatMessageResponse("Created.", true, []), await response.Content.ReadFromJsonAsync<ChatMessageResponse>());
        var workouts = await client.GetFromJsonAsync<JsonElement>("/api/workouts");
        Assert.Equal("Pull day", workouts.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task SendMessage_AnswersFromTheFallbackModel_WhenOpenRouterReturnsAnErrorStatus(int status)
    {
        // Arrange
        var client = await StartAsync(request => request.Model == Primary
            ? FakeOpenRouterReply.Error(status)
            : FakeOpenRouterReply.Text("from fallback"));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        Assert.Equal("from fallback", (await response.Content.ReadFromJsonAsync<ChatMessageResponse>())?.Reply);
        Assert.Equal([Primary, Fallback], _server!.Requests.Select(request => request.Model));
    }

    [Fact]
    public async Task SendMessage_AnswersFromTheFallbackModel_WhenThePrimaryTimesOut()
    {
        // Arrange
        var client = await StartAsync(
            request => request.Model == Primary
                ? FakeOpenRouterReply.Text("too late").After(TimeSpan.FromSeconds(4))
                : FakeOpenRouterReply.Text("from fallback"),
            timeoutSeconds: 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        Assert.Equal("from fallback", (await response.Content.ReadFromJsonAsync<ChatMessageResponse>())?.Reply);
        Assert.Equal([Primary, Fallback], _server!.Requests.Select(request => request.Model));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendMessage_AnswersFromTheFallbackModel_WhenThePrimaryReplyIsEmpty(string? content)
    {
        // Arrange
        var client = await StartAsync(request => request.Model != Primary
            ? FakeOpenRouterReply.Text("from fallback")
            : content is null ? FakeOpenRouterReply.NullContent() : FakeOpenRouterReply.Text(content));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        Assert.Equal("from fallback", (await response.Content.ReadFromJsonAsync<ChatMessageResponse>())?.Reply);
        Assert.Equal([Primary, Fallback], _server!.Requests.Select(request => request.Model));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    public async Task SendMessage_ReturnsProviderUnavailableWithoutFailingOver_WhenOpenRouterRejectsTheRequest(int status)
    {
        // Arrange
        var client = await StartAsync(_ => FakeOpenRouterReply.Error(status));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
        Assert.Equal([Primary], _server!.Requests.Select(request => request.Model));
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenBothModelsFail()
    {
        // Arrange
        var client = await StartAsync(request => request.Model == Primary
            ? FakeOpenRouterReply.Error(429)
            : FakeOpenRouterReply.Error(503));

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
        Assert.Equal([Primary, Fallback], _server!.Requests.Select(request => request.Model));
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenOpenRouterCannotBeReached()
    {
        // Arrange
        var client = await StartAsync(_ => FakeOpenRouterReply.Text("unused"));
        _server!.Dispose();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
    }

    [Fact]
    public async Task SendMessage_ReturnsRateLimitedWithoutCallingOpenRouter_WhenTheUserSendsTooManyMessages()
    {
        // Arrange
        var client = await StartAsync(_ => FakeOpenRouterReply.Text("ok"), settings =>
        {
            settings["AiCoach:RateLimitRequests"] = "2";
            settings["AiCoach:RateLimitWindowSeconds"] = "1";
        });
        var message = new ChatMessageRequest("Hi", null);

        // Act
        var first = await client.PostAsJsonAsync("/api/chat/messages", message);
        var second = await client.PostAsJsonAsync("/api/chat/messages", message);
        var third = await client.PostAsJsonAsync("/api/chat/messages", message);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        var afterWindow = await client.PostAsJsonAsync("/api/chat/messages", message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal(ErrorCodes.ChatRateLimited, (await third.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
        Assert.Equal(HttpStatusCode.OK, afterWindow.StatusCode);
        Assert.Equal(3, _server!.Requests.Count);
    }

    [Fact]
    public async Task SendMessage_StopsCallingOpenRouter_OnceTheDailyTokenBudgetIsSpent()
    {
        // Arrange
        var client = await StartAsync(_ => FakeOpenRouterReply.Text("ok", totalTokens: 100), settings =>
        {
            settings["AiCoach:DailyTokenBudget"] = "150";
            settings["AiCoach:RateLimitRequests"] = "50";
        });
        var message = new ChatMessageRequest("Hi", null);

        // Act
        var first = await client.PostAsJsonAsync("/api/chat/messages", message);
        var second = await client.PostAsJsonAsync("/api/chat/messages", message);
        var third = await client.PostAsJsonAsync("/api/chat/messages", message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal(ErrorCodes.ChatTokenBudgetExceeded, (await third.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
        Assert.Equal(2, _server!.Requests.Count);
    }

    private static async Task AssertProviderUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ChatProviderUnavailable, (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }

    private async Task<HttpClient> StartAsync(
        Func<FakeOpenRouterRequest, FakeOpenRouterReply> handler,
        Action<Dictionary<string, string>>? configure = null,
        int timeoutSeconds = 30)
    {
        _server = new FakeOpenRouterServer(handler);
        _factory.Settings["AiCoach:ApiKey"] = "test-key";
        _factory.Settings["AiCoach:Endpoint"] = _server.Endpoint;
        _factory.Settings["AiCoach:Model"] = Primary;
        _factory.Settings["AiCoach:FallbackModel"] = Fallback;
        _factory.Settings["AiCoach:RequestTimeoutSeconds"] = timeoutSeconds.ToString();
        configure?.Invoke(_factory.Settings);

        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }
}
