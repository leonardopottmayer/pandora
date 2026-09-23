using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Application.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Microsoft.Extensions.Options;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;

/// <summary>
/// The web text pipeline: a sentence → a validated tool call → an executed (or held-for-confirmation)
/// command → a recorded outcome. It loads the user's profile, fetches their key from Integrations, sends
/// the sentence plus the command catalog to the provider, and acts on whatever the model returned. Every
/// path records exactly one invocation into the current conversation — and the reply reflects the
/// command's real result, never a success that did not happen.
/// </summary>
public sealed class InterpretCommandHandler(
    IUnitOfWorkFactory factory,
    IExternalCredentialProvider credentials,
    IAiChatCompletionClientFactory clientFactory,
    IUserPreferencesReader preferences,
    IEffectiveTimeZoneResolver timeZones,
    IEnumerable<IAssistantTool> tools,
    IOptions<AssistantOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<InterpretCommand, InterpretResultDto>
{
    private static readonly TimeSpan ConfirmationWindow = TimeSpan.FromMinutes(10);

    /// <summary>Recorded as the utterance when a voice note could not be turned into text.</summary>
    private const string VoiceNotePlaceholder = "[voice note]";

    private static string TranscriptionPrompt(string locale) =>
        $"Transcribe this voice note verbatim, in the language it was spoken (most likely {locale}). " +
        "Reply with the transcription only: no quotes, no commentary. " +
        "If there is no intelligible speech, reply with nothing.";

    protected override async Task<Result<InterpretResultDto>> HandleAsync(InterpretCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var userId = input.UserId;
        var text = input.Text?.Trim();
        if (string.IsNullOrEmpty(text) && input.Audio is null)
            return Fail(AssistantErrors.EmptyText);

        var profile = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IAssistantProfileRepository>();
            return await repo.FindByUserAsync(userId, token);
        }, cancellationToken: ct);

        if (profile is null || !profile.IsEnabled)
            return Fail(AssistantErrors.NotEnabled);

        var keyResult = await credentials.GetApiKeyAsync(userId, profile.ChatProvider, ct);
        if (!keyResult.IsSuccess)
            return Fail(AssistantErrors.NoApiKey(profile.ChatProvider));

        var now = timeProvider.GetUtcNow();
        var (conversation, isNewConversation) = await ResolveConversationAsync(userId, input.ConversationId, now, ct);

        // Reference clock for resolving relative dates. The zone goes through the effective resolver so
        // a user with no preferences row (e.g. one who only ever used Telegram) gets the configured
        // account default instead of UTC — the difference between "22h" meaning 22:00-03:00 and 22:00Z.
        var prefs = await preferences.GetAsync(userId, ct);
        var timeZone = await timeZones.ResolveAsync(userId, ct: ct);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var locale = string.IsNullOrWhiteSpace(profile.LocaleOverride) ? "pt-BR" : profile.LocaleOverride!;

        var client = clientFactory.GetClient(profile.ChatProvider);
        var start = timeProvider.GetTimestamp();

        // A voice note is transcribed by its own call first, so what was heard is logged as the
        // utterance and echoed back, and the rest of the pipeline stays identical to typed text. The
        // audio bytes are not retained. Its tokens and latency are folded into the invocation.
        string? transcript = null;
        var transcriptionTokens = new TokenUsage(0, 0);
        if (input.Audio is { } audio)
        {
            try
            {
                var transcription = await client.CompleteAsync(new ChatRequest(
                    profile.ChatModel,
                    [ChatMessage.User(TranscriptionPrompt(locale), audio)],
                    Temperature: 0,
                    ApiKey: keyResult.Value), ct);
                transcript = transcription.Message.Content?.Trim();
                transcriptionTokens = transcription.Usage;
            }
            catch (AiException ex)
            {
                return await RecordAsync(null, Outcome(
                    conversation, isNewConversation, now, userId, VoiceNotePlaceholder, InvocationStatus.ProviderError,
                    commandName: null, argumentsJson: null, result: null, error: ex.Message,
                    profile, (long)timeProvider.GetElapsedTime(start).TotalMilliseconds,
                    promptTokens: 0, completionTokens: 0, expiresAt: null), ct);
            }

            if (string.IsNullOrEmpty(transcript))
                return await RecordAsync(null, Outcome(
                    conversation, isNewConversation, now, userId, VoiceNotePlaceholder, InvocationStatus.Clarification,
                    commandName: null, argumentsJson: null, result: "I couldn't make out the voice note. Could you try again?",
                    error: null, profile, (long)timeProvider.GetElapsedTime(start).TotalMilliseconds,
                    transcriptionTokens.PromptTokens, transcriptionTokens.CompletionTokens, expiresAt: null), ct);

            text = transcript;
        }

        // Non-empty from here on: either typed text or a transcript (checked above).
        var utterance = text!;

        var toolsByName = tools.ToDictionary(t => t.Descriptor.Name, StringComparer.Ordinal);
        var descriptors = toolsByName.Values.Select(t => t.Descriptor).ToList();
        var toolDefinitions = descriptors
            .Select(d => new ToolDefinition(d.Name, d.Description, d.ParametersJsonSchema))
            .ToList();

        var systemPrompt = AssistantSystemPrompt.Build(
            localNow, timeZone.Id, prefs?.WeekStartsOn ?? DayOfWeek.Monday, locale, descriptors);

        // Multi-turn: re-send the active conversation's recent turns so a follow-up ("sim", "muda pra
        // 11h") is understood. The active-conversation window bounds it in time; the limit bounds tokens.
        var history = await LoadHistoryAsync(conversation, isNewConversation, ct);

        var messages = new List<ChatMessage>(history.Count + 2) { ChatMessage.System(systemPrompt) };
        foreach (var past in history)
        {
            messages.Add(past.Author == MessageAuthor.Assistant
                ? new ChatMessage(ChatRole.Assistant, past.Content)
                : ChatMessage.User(past.Content));
        }
        messages.Add(ChatMessage.User(utterance));

        var chatRequest = new ChatRequest(
            profile.ChatModel,
            messages,
            Tools: toolDefinitions,
            Temperature: 0,
            ApiKey: keyResult.Value);

        ChatCompletion completion;
        try
        {
            completion = await client.CompleteAsync(chatRequest, ct);
        }
        catch (AiException ex)
        {
            var latencyMs = (long)timeProvider.GetElapsedTime(start).TotalMilliseconds;
            return await RecordAsync(transcript, Outcome(
                conversation, isNewConversation, now, userId, utterance, InvocationStatus.ProviderError,
                commandName: null, argumentsJson: null, result: null, error: ex.Message,
                profile, latencyMs, promptTokens: 0, completionTokens: 0, expiresAt: null), ct);
        }

        var latency = (long)timeProvider.GetElapsedTime(start).TotalMilliseconds;
        var usage = new TokenUsage(
            completion.Usage.PromptTokens + transcriptionTokens.PromptTokens,
            completion.Usage.CompletionTokens + transcriptionTokens.CompletionTokens);

        // The model replied in prose — it is asking a question or declining. Nothing runs.
        var toolCall = completion.ToolCalls.Count > 0 ? completion.ToolCalls[0] : null;
        if (toolCall is null)
        {
            var message = string.IsNullOrWhiteSpace(completion.Message.Content)
                ? "I didn't understand. Could you rephrase?"
                : completion.Message.Content!;
            return await RecordAsync(transcript, Outcome(
                conversation, isNewConversation, now, userId, utterance, InvocationStatus.Clarification,
                commandName: null, argumentsJson: null, result: message, error: null,
                profile, latency, usage.PromptTokens, usage.CompletionTokens, expiresAt: null), ct);
        }

        var argumentsJson = toolCall.Arguments.GetRawText();

        // The model named a tool the catalog does not have.
        if (!toolsByName.TryGetValue(toolCall.Name, out var tool))
            return await RecordAsync(transcript, Outcome(
                conversation, isNewConversation, now, userId, utterance, InvocationStatus.Rejected,
                toolCall.Name, argumentsJson, result: null, error: $"Unknown command '{toolCall.Name}'.",
                profile, latency, usage.PromptTokens, usage.CompletionTokens, expiresAt: null), ct);

        // The command's policy, shifted by the user's confirmation level, may hold the tool call for
        // confirmation instead of running it now.
        if (RequiresConfirmation(tool.Descriptor.Confirmation, profile.ConfirmationLevel))
        {
            var intent = $"Confirm {toolCall.Name}? Arguments: {argumentsJson}";
            return await RecordAsync(transcript, Outcome(
                conversation, isNewConversation, now, userId, utterance, InvocationStatus.PendingConfirmation,
                toolCall.Name, argumentsJson, result: intent, error: null,
                profile, latency, usage.PromptTokens, usage.CompletionTokens, expiresAt: now + ConfirmationWindow), ct);
        }

        AssistantCommandOutcome commandOutcome;
        try
        {
            commandOutcome = await tool.ExecuteAsync(userId, toolCall.Arguments, ct);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            // Malformed or missing arguments that slipped past the schema — a write-time rejection.
            return await RecordAsync(transcript, Outcome(
                conversation, isNewConversation, now, userId, utterance, InvocationStatus.Rejected,
                toolCall.Name, argumentsJson, result: null, error: ex.Message,
                profile, latency, usage.PromptTokens, usage.CompletionTokens, expiresAt: null), ct);
        }

        var status = commandOutcome.Success ? InvocationStatus.Executed : InvocationStatus.Failed;
        return await RecordAsync(transcript, Outcome(
            conversation, isNewConversation, now, userId, utterance, status,
            toolCall.Name, argumentsJson,
            result: commandOutcome.Success ? commandOutcome.Message : null,
            error: commandOutcome.Success ? null : commandOutcome.Message,
            profile, latency, usage.PromptTokens, usage.CompletionTokens, expiresAt: null), ct);
    }

    /// <summary>
    /// The recent turns of the active conversation, oldest-first, to prepend as context. Empty for a new
    /// conversation (nothing to recall) or when the limit is off.
    /// </summary>
    private async Task<IReadOnlyList<Message>> LoadHistoryAsync(
        Conversation conversation, bool isNewConversation, CancellationToken ct)
    {
        var limit = options.Value.HistoryMessageLimit;
        if (isNewConversation || limit <= 0)
            return [];

        return await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
            await context.AcquireRepository<IMessageRepository>()
                .GetRecentByConversationAsync(conversation.Id, limit, token),
            cancellationToken: ct);
    }

    private async Task<(Conversation Conversation, bool IsNew)> ResolveConversationAsync(
        Guid userId, Guid? conversationId, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IConversationRepository>();
            return conversationId is { } id
                ? await repo.GetByIdAsync(id, token)
                : await repo.FindMostRecentByUserAsync(userId, token);
        }, cancellationToken: ct);

        if (existing is not null && existing.UserId == userId && !existing.IsExpired(now))
            return (existing, false);

        return (Conversation.Start(userId, timeProvider), true);
    }

    private async Task<Result<InterpretResultDto>> RecordAsync(string? transcript, InvocationOutcome outcome, CancellationToken ct)
    {
        var assistantContent = outcome.Result ?? outcome.Error ?? string.Empty;

        var invocation = CommandInvocation.Create(
            outcome.UserId, outcome.Conversation.Id, outcome.Utterance, outcome.CommandName, outcome.ArgumentsJson,
            outcome.Status, outcome.Result, outcome.Error,
            outcome.Provider, outcome.Model, outcome.LatencyMs, outcome.PromptTokens, outcome.CompletionTokens,
            outcome.ExpiresAt, timeProvider);

        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var conversations = context.AcquireRepository<IConversationRepository>();
            var messages = context.AcquireRepository<IMessageRepository>();
            var invocations = context.AcquireRepository<ICommandInvocationRepository>();

            outcome.Conversation.Touch(outcome.Now);
            if (outcome.IsNewConversation)
                await conversations.AddAsync(outcome.Conversation, token);
            else
                await conversations.UpdateAsync(outcome.Conversation, token);

            await messages.AddAsync(
                Message.Create(outcome.Conversation.Id, MessageAuthor.User, outcome.Utterance, timeProvider), token);
            await messages.AddAsync(
                Message.Create(outcome.Conversation.Id, MessageAuthor.Assistant, assistantContent, timeProvider), token);

            await invocations.AddAsync(invocation, token);
            return true;
        }, cancellationToken: ct);

        return Ok(new InterpretResultDto(
            invocation.Id, outcome.Conversation.Id, outcome.Status.Value,
            outcome.CommandName, outcome.ArgumentsJson, assistantContent, transcript));
    }

    /// <summary>True when the command must be confirmed before running, once the level shifts its policy.</summary>
    private static bool RequiresConfirmation(ConfirmationPolicy policy, ConfirmationLevel level) =>
        Shift(policy, level) == ConfirmationPolicy.Always;

    private static ConfirmationPolicy Shift(ConfirmationPolicy policy, ConfirmationLevel level)
    {
        if (level == ConfirmationLevel.Strict)
            return policy switch
            {
                ConfirmationPolicy.Never => ConfirmationPolicy.WhenAmbiguous,
                _ => ConfirmationPolicy.Always,
            };

        if (level == ConfirmationLevel.Trusting)
            return policy switch
            {
                ConfirmationPolicy.Always => ConfirmationPolicy.WhenAmbiguous,
                _ => ConfirmationPolicy.Never,
            };

        return policy; // Balanced: as declared.
    }

    private static InvocationOutcome Outcome(
        Conversation conversation, bool isNewConversation, DateTimeOffset now,
        Guid userId, string utterance, InvocationStatus status,
        string? commandName, string? argumentsJson, string? result, string? error,
        AssistantProfile profile, long latencyMs, int promptTokens, int completionTokens,
        DateTimeOffset? expiresAt) =>
        new(conversation, isNewConversation, now, userId, utterance, status, commandName, argumentsJson,
            result, error, profile.ChatProvider, profile.ChatModel, latencyMs, promptTokens, completionTokens, expiresAt);

    private sealed record InvocationOutcome(
        Conversation Conversation,
        bool IsNewConversation,
        DateTimeOffset Now,
        Guid UserId,
        string Utterance,
        InvocationStatus Status,
        string? CommandName,
        string? ArgumentsJson,
        string? Result,
        string? Error,
        string Provider,
        string Model,
        long LatencyMs,
        int PromptTokens,
        int CompletionTokens,
        DateTimeOffset? ExpiresAt);
}
