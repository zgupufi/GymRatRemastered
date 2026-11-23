using GymRatService.API.Validators;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Enums;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.BLL.Tests.Validators;

public class ChatMessageRequestValidatorTests
{
    private readonly ChatMessageRequestValidator _validator = new(Options.Create(new AiCoachSettings()));

    private static List<ChatHistoryItem> History(int count) =>
        Enumerable.Range(0, count).Select(_ => new ChatHistoryItem(ChatRole.User, "hello")).ToList();

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public async Task Validate_Passes_WhenTheHistoryIsWithinTheLimit(int count)
    {
        // Act
        var result = await _validator.ValidateAsync(new ChatMessageRequest("Hi", History(count)));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Passes_WhenTheHistoryIsMissing()
    {
        // Act
        var result = await _validator.ValidateAsync(new ChatMessageRequest("Hi", null));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Fails_WhenTheHistoryHasMoreThanTwentyItems()
    {
        // Act
        var result = await _validator.ValidateAsync(new ChatMessageRequest("Hi", History(21)));

        // Assert
        Assert.Equal("History", Assert.Single(result.Errors).PropertyName);
    }
}
