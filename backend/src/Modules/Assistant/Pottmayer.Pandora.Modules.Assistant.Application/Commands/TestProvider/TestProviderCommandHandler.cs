using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.TestProvider;

/// <summary>
/// The reachability test behind the settings screen: fetch the user's key from Integrations, send one
/// tiny prompt to the provider, and report whether it answered and how fast. The command itself always
/// succeeds — the payload carries the probe's outcome, so the UI can show "no key", "rejected" or
/// "unreachable" distinctly (via <c>AiException.IsPermanent</c>).
/// </summary>
public sealed class TestProviderCommandHandler(
    IExternalCredentialProvider credentials,
    IAiChatCompletionClientFactory clientFactory,
    TimeProvider timeProvider)
    : CommandHandlerBase<TestProviderCommand, ReachabilityResultDto>
{
    protected override async Task<Result<ReachabilityResultDto>> HandleAsync(TestProviderCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var provider = string.IsNullOrWhiteSpace(input.Provider) ? AssistantDefaults.Provider : input.Provider.Trim();

        var keyResult = input.AccountId is { } accountId
            ? await credentials.GetApiKeyAsync(input.UserId, accountId, ct)
            : null;
        if (keyResult is not { IsSuccess: true })
            return Ok(new ReachabilityResultDto(
                Ok: false, LatencyMs: 0, Reply: null,
                Error: $"No '{provider}' key selected. Pick one, or add it under Integrations.",
                ErrorKind: "no_key"));

        var model = string.IsNullOrWhiteSpace(input.Model) ? AssistantDefaults.Model : input.Model!.Trim();
        var client = clientFactory.GetClient(provider);

        var chatRequest = new ChatRequest(
            model,
            [ChatMessage.User("Reply only: ok")],
            Temperature: 0,
            ApiKey: keyResult.Value);

        var start = timeProvider.GetTimestamp();
        try
        {
            var completion = await client.CompleteAsync(chatRequest, ct);
            var latency = (long)timeProvider.GetElapsedTime(start).TotalMilliseconds;
            return Ok(new ReachabilityResultDto(true, latency, completion.Message.Content, null, null));
        }
        catch (AiException ex)
        {
            var latency = (long)timeProvider.GetElapsedTime(start).TotalMilliseconds;
            return Ok(new ReachabilityResultDto(
                Ok: false, LatencyMs: latency, Reply: null,
                Error: ex.Message,
                ErrorKind: ex.IsPermanent ? "rejected" : "unreachable"));
        }
    }
}
