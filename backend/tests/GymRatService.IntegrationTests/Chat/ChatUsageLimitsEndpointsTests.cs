using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Service.Usage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using ChatRole = GymRatService.Common.Enums.ChatRole;

namespace GymRatService.IntegrationTests.Chat;

public sealed class ChatUsageLimitsEndpointsTests : IDisposable
{
    private const int MaximumOutputTokens = 600;

    private static readonly ChatMessageRequest Message = new("Hi", null);

    private readonly GymRatApiFactory _factory = new();
    private readonly ScriptedChatClient _chat = new();

    public ChatUsageLimitsEndpointsTests()
    {
        _factory.ChatClient = _chat;
        _chat.Script = (_, _) => ScriptedChatClient.Reply("ok");
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SendMessage_KeepsTheTokenBudgetAfterARestart()
    {
        // Arrange
        _chat.Script = (_, _) => ScriptedChatClient.Reply("ok", totalTokens: 30000);
        var token = await RegisterAsync();
        var first = await _factory.CreateBearerClient(token).PostAsJsonAsync("/api/chat/messages", Message);
        using var restarted = Restart();

        // Act
        var second = await CreateClient(restarted, token).PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertErrorAsync(second, ErrorCodes.ChatTokenBudgetExceeded);
        Assert.Single(_chat.Calls);
    }

    [Theory]
    [InlineData("AiCoach:RateLimitRequests")]
    [InlineData("AiCoach:DailyRequestLimit")]
    public async Task SendMessage_KeepsTheRequestLimitsAfterARestart(string limit)
    {
        // Arrange
        _factory.Settings[limit] = "1";
        var token = await RegisterAsync();
        var first = await _factory.CreateBearerClient(token).PostAsJsonAsync("/api/chat/messages", Message);
        using var restarted = Restart();

        // Act
        var second = await CreateClient(restarted, token).PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertErrorAsync(second, ErrorCodes.ChatRateLimited);
        Assert.Single(_chat.Calls);
    }

    [Fact]
    public async Task SendMessage_AllowsMessagesAgainOnTheNextDay()
    {
        // Arrange
        _chat.Script = (_, _) => ScriptedChatClient.Reply("ok", totalTokens: 30000);
        var client = _factory.CreateBearerClient(await RegisterAsync());
        await client.PostAsJsonAsync("/api/chat/messages", Message);
        var sameDay = await client.PostAsJsonAsync("/api/chat/messages", Message);
        _factory.Time.Advance(TimeSpan.FromDays(1));

        // Act
        var nextDay = await client.PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        await AssertErrorAsync(sameDay, ErrorCodes.ChatTokenBudgetExceeded);
        Assert.Equal("ok", (await nextDay.Content.ReadFromJsonAsync<ChatMessageResponse>())?.Reply);
    }

    [Fact]
    public async Task SendMessage_RefusesBeforeCallingTheProvider_WhenTheEstimateDoesNotFitTheBudget()
    {
        // Arrange
        _factory.Settings["AiCoach:DailyTokenBudget"] = "700";
        var client = _factory.CreateBearerClient(await RegisterAsync());

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        await AssertErrorAsync(response, ErrorCodes.ChatTokenBudgetExceeded);
        Assert.Empty(_chat.Calls);
    }

    [Fact]
    public async Task SendMessage_RefusesALongHistoryThatNoLongerFitsTheBudget()
    {
        // Arrange
        var token = await RegisterAsync();
        await _factory.CreateBearerClient(token).PostAsJsonAsync("/api/chat/messages", Message);
        using var restarted = Restart(budget: 2 * FirstInputEstimate() + MaximumOutputTokens + 1000);
        var client = CreateClient(restarted, token);
        var history = new[] { new ChatHistoryItem(ChatRole.User, new string('a', 8000)) };

        // Act
        var withHistory = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", history));
        var withoutHistory = await client.PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        await AssertErrorAsync(withHistory, ErrorCodes.ChatTokenBudgetExceeded);
        Assert.Equal(HttpStatusCode.OK, withoutHistory.StatusCode);
        Assert.Equal(2, _chat.Calls.Count);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task SendMessage_ChargesAnEstimate_WhenTheProviderReportsNoUsage(int spareTokens, bool accepted)
    {
        // Arrange
        _chat.Script = (_, _) => new ChatResponse(new ChatMessage(AiChatRole.Assistant, "ok"));
        var token = await RegisterAsync();
        await _factory.CreateBearerClient(token).PostAsJsonAsync("/api/chat/messages", Message);
        using var restarted = Restart(budget: 2 * FirstInputEstimate() + MaximumOutputTokens + spareTokens);

        // Act
        var second = await CreateClient(restarted, token).PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task SendMessage_ReleasesTheReservedTokens_WhenTheProviderFails()
    {
        // Arrange
        _chat.Script = (_, _) => throw new HttpRequestException("boom");
        var token = await RegisterAsync();
        var failed = await _factory.CreateBearerClient(token).PostAsJsonAsync("/api/chat/messages", Message);
        var estimate = FirstInputEstimate();
        _chat.Script = (_, _) => ScriptedChatClient.Reply("ok");
        using var restarted = Restart(budget: estimate + MaximumOutputTokens);

        // Act
        var second = await CreateClient(restarted, token).PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        await AssertErrorAsync(failed, ErrorCodes.ChatProviderUnavailable);
        Assert.Equal("ok", (await second.Content.ReadFromJsonAsync<ChatMessageResponse>())?.Reply);
    }

    [Fact]
    public async Task SendMessage_CountsEveryModelCall_IncludingRepliesTheFailoverDiscards()
    {
        // Arrange
        _chat.Script = (_, options) => options?.ModelId == "primary-model"
            ? ScriptedChatClient.Reply("", totalTokens: 29000)
            : ScriptedChatClient.Reply("from fallback", totalTokens: 1000);
        _factory.Settings["AiCoach:Model"] = "primary-model";
        _factory.Settings["AiCoach:FallbackModel"] = "fallback-model";
        var client = _factory.CreateBearerClient(await RegisterAsync());

        // Act
        var first = await client.PostAsJsonAsync("/api/chat/messages", Message);
        var second = await client.PostAsJsonAsync("/api/chat/messages", Message);

        // Assert
        Assert.Equal("from fallback", (await first.Content.ReadFromJsonAsync<ChatMessageResponse>())?.Reply);
        await AssertErrorAsync(second, ErrorCodes.ChatTokenBudgetExceeded);
    }

    [Fact]
    public async Task SendMessage_SendsOnlyTheNewestHistoryThatFitsTheCharacterLimit()
    {
        // Arrange
        _factory.Settings["AiCoach:MaximumHistoryCharacters"] = "10";
        var client = _factory.CreateBearerClient(await RegisterAsync());
        var history = new[] { "a", "far too long to fit", "123456", "7890" }
            .Select(content => new ChatHistoryItem(ChatRole.User, content))
            .ToList();

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", new ChatMessageRequest("Hi", history));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["123456", "7890", "Hi"], _chat.Calls[0].Skip(1).Select(message => message.Text));
    }

    [Fact]
    public async Task DeleteAccount_Succeeds_WhenTheUserHasCoachUsage()
    {
        // Arrange
        var client = _factory.CreateBearerClient(await RegisterAsync());
        await client.PostAsJsonAsync("/api/chat/messages", Message);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new { password = "password123" }),
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private long FirstInputEstimate() => ChatTokenEstimator.EstimateInput(_chat.Calls[0], _chat.Options[0]);

    private WebApplicationFactory<Program> Restart(long? budget = null) =>
        _factory.WithWebHostBuilder(builder =>
        {
            if (budget is { } tokens)
            {
                builder.UseSetting("AiCoach:DailyTokenBudget", tokens.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    private async Task<string> RegisterAsync()
    {
        await _factory.SeedAsync(_ => { });

        return await _factory.RegisterAndLoginAsync(new RegisterRequest("ana@example.com", "password123", "Ana", null, WeightUnit.Kilograms));
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }
}
