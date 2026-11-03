using System.ClientModel;
using System.Net;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;

namespace GymRatService.IntegrationTests.Chat;

public sealed class ChatFailoverEndpointsTests : IDisposable
{
    private const string Primary = "primary-model";

    private const string Fallback = "fallback-model";

    private readonly GymRatApiFactory _factory = new();
    private readonly ScriptedChatClient _chat = new();

    public ChatFailoverEndpointsTests()
    {
        _factory.ChatClient = _chat;
        _factory.Settings["AiCoach:Model"] = Primary;
        _factory.Settings["AiCoach:FallbackModel"] = Fallback;
    }

    public void Dispose() => _factory.Dispose();

    public static TheoryData<Exception> FailoverExceptions() =>
    [
        new HttpRequestException("too many", null, HttpStatusCode.TooManyRequests),
        new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway),
        new HttpRequestException("network down"),
        new TimeoutException(),
        new TaskCanceledException("timed out"),
        new ClientResultException(new StatusPipelineResponse(429)),
        new ClientResultException(new StatusPipelineResponse(503)),
        new ClientResultException("no response"),
    ];

    [Theory]
    [MemberData(nameof(FailoverExceptions))]
    public async Task SendMessage_AnswersFromTheFallbackModel_WhenThePrimaryFailsTransiently(Exception failure)
    {
        // Arrange
        _chat.Script = (_, options) => options?.ModelId == Primary
            ? throw failure
            : ScriptedChatClient.Reply("from fallback");
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal("from fallback", body?.Reply);
        Assert.Equal([Primary, Fallback], _chat.Options.Select(options => options?.ModelId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_AnswersFromTheFallbackModel_WhenThePrimaryReplyIsEmpty(string emptyReply)
    {
        // Arrange
        _chat.Script = (_, options) => ScriptedChatClient.Reply(options?.ModelId == Primary ? emptyReply : "from fallback");
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal("from fallback", body?.Reply);
    }

    [Fact]
    public async Task SendMessage_DoesNotTreatAToolCallAsAnEmptyReply()
    {
        // Arrange
        _chat.Script = (messages, _) => ScriptedChatClient.ToolResults(messages).Any()
            ? ScriptedChatClient.Reply("listed")
            : ScriptedChatClient.CallTool("list_workouts");
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("List", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal("listed", body?.Reply);
        Assert.All(_chat.Options, options => Assert.Equal(Primary, options?.ModelId));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    public async Task SendMessage_ReturnsProviderUnavailableWithoutFailingOver_WhenThePrimaryRejectsTheRequest(int status)
    {
        // Arrange
        _chat.Script = (_, _) => throw new HttpRequestException("rejected", null, (HttpStatusCode)status);
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
        Assert.Single(_chat.Calls);
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailableWithoutFailingOver_WhenTheErrorIsNotATransportError()
    {
        // Arrange
        _chat.Script = (_, _) => throw new InvalidOperationException("unexpected");
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
        Assert.Single(_chat.Calls);
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenBothModelsFail()
    {
        // Arrange
        _chat.Script = (_, _) => throw new TimeoutException();
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
        Assert.Equal(2, _chat.Calls.Count);
    }

    [Fact]
    public async Task SendMessage_ReturnsProviderUnavailable_WhenNoFallbackIsConfiguredAndThePrimaryFails()
    {
        // Arrange
        _factory.Settings["AiCoach:FallbackModel"] = "";
        _chat.Script = (_, _) => throw new TimeoutException();
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        await AssertProviderUnavailableAsync(response);
        Assert.Single(_chat.Calls);
    }

    [Fact]
    public async Task SendMessage_ReturnsTheEmptyReply_WhenNoFallbackIsConfigured()
    {
        // Arrange
        _factory.Settings["AiCoach:FallbackModel"] = "";
        _chat.Script = (_, _) => ScriptedChatClient.Reply("");
        var client = await CreateClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", null));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponse>();
        Assert.Equal("", body?.Reply);
        Assert.Single(_chat.Calls);
    }

    [Fact]
    public async Task SendMessage_ReturnsRateLimited_WhenTheDailyRequestLimitIsReached()
    {
        // Arrange
        _chat.Script = (_, _) => ScriptedChatClient.Reply("ok");
        _factory.Settings["AiCoach:DailyRequestLimit"] = "2";
        var client = await CreateClientAsync();
        var message = new ChatMessageRequest("Hi", null);

        // Act
        var first = await client.PostAsJsonAsync("/api/chat/messages", message);
        _factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var second = await client.PostAsJsonAsync("/api/chat/messages", message);
        _factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var third = await client.PostAsJsonAsync("/api/chat/messages", message);
        _factory.Clock.Advance(TimeSpan.FromDays(1));
        var nextDay = await client.PostAsJsonAsync("/api/chat/messages", message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal(ErrorCodes.ChatRateLimited, (await third.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
        Assert.Equal(HttpStatusCode.OK, nextDay.StatusCode);
    }

    private static async Task AssertProviderUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ChatProviderUnavailable, (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        await _factory.SeedAsync(_ => { });
        var accessToken = await _factory.RegisterAndLoginAsync(new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));

        return _factory.CreateBearerClient(accessToken);
    }
}
