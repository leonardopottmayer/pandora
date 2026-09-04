using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Tars.Ai.Chat.DI;
using Pottmayer.Tars.Ai.Chat.Gemini.DI;

namespace Pottmayer.Pandora.Modules.Assistant.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddAssistantInfrastructure(this IHostApplicationBuilder builder)
    {
        // Tars AI transport. The Gemini chat client is selected per call through the keyed factory. No
        // default API key is configured: each user's key comes from Integrations and is passed on the
        // request (ChatRequest.ApiKey). The options section (Tars:Ai:Chat:Gemini) only carries the base
        // URL and timeout.
        builder.AddTarsAiChatGeminiOptions();
        builder.Services.AddTarsAiChatGeminiHttpClient();
        builder.Services.AddTarsAiChatCompletionClientGemini();
        builder.Services.AddTarsAiClientFactory();

        return builder;
    }
}
