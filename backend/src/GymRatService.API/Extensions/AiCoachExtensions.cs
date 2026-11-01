using System.ClientModel;
using GymRatService.Common.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace GymRatService.API.Extensions;

public static class AiCoachExtensions
{
    public static IServiceCollection AddAiCoach(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiCoachSettings>(configuration.GetSection(AiCoachSettings.SectionName));

        services.AddSingleton<IChatClient>(serviceProvider =>
            CreateChatClient(serviceProvider.GetRequiredService<IOptions<AiCoachSettings>>().Value));

        return services;
    }

    private static IChatClient CreateChatClient(AiCoachSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new UnconfiguredChatClient();
        }

        var client = new OpenAIClient(
            new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(settings.Endpoint) });

        return client.GetChatClient(settings.Model).AsIChatClient();
    }
}
