using FluentValidation;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Validators;

public class ChatMessageRequestValidator : AbstractValidator<ChatMessageRequest>
{
    private const int MaximumHistoryContentLength = 8000;

    public ChatMessageRequestValidator(IOptions<AiCoachSettings> options)
    {
        RuleFor(request => request.Message)
            .NotEmpty()
            .MaximumLength(options.Value.MaximumMessageLength);

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
