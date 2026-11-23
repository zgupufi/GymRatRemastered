using FluentValidation;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Validators;

public class ChatMessageRequestValidator : AbstractValidator<ChatMessageRequest>
{
    private const int MaximumHistoryContentLength = 8000;

    private const int MaximumHistoryItems = 20;

    public ChatMessageRequestValidator(IOptions<AiCoachSettings> options)
    {
        RuleFor(request => request.Message)
            .NotEmpty()
            .MaximumLength(options.Value.MaximumMessageLength);

        RuleFor(request => request.History)
            .Must(history => history is not { Count: > MaximumHistoryItems });

        RuleForEach(request => request.History)
            .ChildRules(item =>
            {
                item.RuleFor(historyItem => historyItem.Role)
                    .IsInEnum();

                item.RuleFor(historyItem => historyItem.Content)
                    .NotEmpty()
                    .MaximumLength(MaximumHistoryContentLength);
            });
    }
}
