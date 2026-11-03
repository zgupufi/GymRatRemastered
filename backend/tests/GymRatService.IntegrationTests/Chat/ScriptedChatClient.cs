using System.Text.Json;
using Microsoft.Extensions.AI;

namespace GymRatService.IntegrationTests.Chat;

public sealed class ScriptedChatClient : IChatClient
{
    public Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Script { get; set; } = (_, _) => Reply("unscripted");

    public List<List<ChatMessage>> Calls { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public static ChatResponse Reply(string text, long totalTokens = 0) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { Usage = new UsageDetails { TotalTokenCount = totalTokens } };

    public static ChatResponse CallTool(string name, object? arguments = null)
    {
        var json = JsonSerializer.SerializeToElement(arguments ?? new { });
        var values = json.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString(), name, values)]));
    }

    public static IEnumerable<object?> ToolResults(IEnumerable<ChatMessage> messages) =>
        messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.Result);

    public static List<JsonElement> ToolResultElements(IEnumerable<ChatMessage> messages) =>
        [.. ToolResults(messages).Select(result => JsonSerializer.SerializeToElement(result))];

    public static JsonElement LastToolResult(IEnumerable<ChatMessage> messages) =>
        JsonSerializer.SerializeToElement(ToolResults(messages).Last());

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = messages.ToList();
        Calls.Add(snapshot);
        Options.Add(options);

        return Task.FromResult(Script(snapshot, options));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
