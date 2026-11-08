using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.CompleteText;

/// <summary>Calls the configured provider without tools, history, or persisted prompts.</summary>
public sealed class CompleteTextCommandHandler(
    IUnitOfWorkFactory factory,
    IExternalCredentialProvider credentials,
    IAiChatCompletionClientFactory clientFactory) : CommandHandlerBase<CompleteTextCommand, string>
{
    protected override async Task<Result<string>> HandleAsync(CompleteTextCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var profile = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
            await context.AcquireRepository<IAssistantProfileRepository>().FindByUserAsync(input.UserId, token),
            cancellationToken: ct);

        if (profile is null || !profile.IsEnabled)
            return Fail(AssistantErrors.NotEnabled);
        if (profile.ChatAccountId is not { } accountId)
            return Fail(AssistantErrors.NoApiKey(profile.ChatProvider));
        var key = await credentials.GetApiKeyAsync(input.UserId, accountId, ct);
        if (!key.IsSuccess)
            return Fail(AssistantErrors.NoApiKey(profile.ChatProvider));

        try
        {
            var completion = await clientFactory.GetClient(profile.ChatProvider).CompleteAsync(new ChatRequest(
                profile.ChatModel, [ChatMessage.System(input.Instruction), ChatMessage.User(input.Text)],
                Temperature: 0, ApiKey: key.Value), ct);
            var text = completion.Message.Content?.Trim();
            return string.IsNullOrEmpty(text) || text.Length > 16_000
                ? Fail(AssistantErrors.TextCompletionFailed)
                : Ok(text);
        }
        catch (AiException)
        {
            return Fail(AssistantErrors.TextCompletionFailed);
        }
        catch (HttpRequestException)
        {
            return Fail(AssistantErrors.TextCompletionFailed);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail(AssistantErrors.TextCompletionFailed);
        }
    }
}
