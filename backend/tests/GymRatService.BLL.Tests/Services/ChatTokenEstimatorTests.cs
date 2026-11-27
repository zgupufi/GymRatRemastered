using GymRatService.Service.Usage;
using Microsoft.Extensions.AI;

namespace GymRatService.BLL.Tests.Services;

public class ChatTokenEstimatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(4001, 1001)]
    public void EstimateInput_CountsFourCharactersAsOneTokenRoundingUp(int characters, long expected)
    {
        // Arrange
        var messages = new[] { new ChatMessage(ChatRole.User, new string('a', characters)) };

        // Act
        var tokens = ChatTokenEstimator.EstimateInput(messages, null);

        // Assert
        Assert.Equal(expected, tokens);
    }

    [Fact]
    public void EstimateInput_IncludesTheToolDefinitions()
    {
        // Arrange
        var tool = AIFunctionFactory.Create(() => "ok", "list_things", "Lists things.");
        var characters = tool.Name.Length + tool.Description.Length + tool.JsonSchema.GetRawText().Length;

        // Act
        var tokens = ChatTokenEstimator.EstimateInput([], new ChatOptions { Tools = [tool] });

        // Assert
        Assert.Equal((characters + 3) / 4, tokens);
    }

    [Fact]
    public void EstimateInput_IncludesToolCallsAndTheirResults()
    {
        // Arrange
        var messages = new[]
        {
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("1", "read", new Dictionary<string, object?> { ["id"] = 12 })]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("1", new string('r', 37))]),
            new ChatMessage(ChatRole.User, [new DataContent(new byte[] { 1, 2, 3 }, "image/png")]),
        };

        // Act
        var tokens = ChatTokenEstimator.EstimateInput(messages, new ChatOptions());

        // Assert
        Assert.Equal(13, tokens);
    }
}
