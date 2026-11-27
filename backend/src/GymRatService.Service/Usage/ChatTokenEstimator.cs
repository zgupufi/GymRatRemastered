using System.Text.Json;
using Microsoft.Extensions.AI;

namespace GymRatService.Service.Usage;

public static class ChatTokenEstimator
{
    private const int CharactersPerToken = 4;

    public static long EstimateInput(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var messageCharacters = messages.SelectMany(message => message.Contents).Sum(CountCharacters);
        var toolCharacters = options?.Tools?.OfType<AIFunctionDeclaration>()
            .Sum(tool => (long)tool.Name.Length + tool.Description.Length + tool.JsonSchema.GetRawText().Length) ?? 0;

        return ToTokens(messageCharacters + toolCharacters);
    }

    public static long EstimateOutput(ChatResponse response) =>
        ToTokens(response.Messages.SelectMany(message => message.Contents).Sum(CountCharacters));

    private static long CountCharacters(AIContent content) => content switch
    {
        TextContent text => text.Text.Length,
        FunctionCallContent call => call.Name.Length + JsonSerializer.Serialize(call.Arguments).Length,
        FunctionResultContent result => JsonSerializer.Serialize(result.Result).Length,
        _ => 0,
    };

    private static long ToTokens(long characters) => (characters + CharactersPerToken - 1) / CharactersPerToken;
}
