using System.Text.Json;
using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;
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
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;

/// <summary>
/// The interpret pipeline: a sentence (typed, or transcribed from a voice note) → validated tool calls →
/// executed (or held-for-confirmation) commands → recorded outcomes. It loads the user's profile, fetches
/// their key from Integrations, sends the sentence plus the command catalog to the provider, and acts on
/// every tool call the model returned. Every path records at least one invocation into the current
/// conversation — one per tool call — and the reply reflects each command's real result, in the user's
/// locale, never a success that did not happen.
/// </summary>
public sealed partial class InterpretCommandHandler(
    IUnitOfWorkFactory factory,
    IExternalCredentialProvider credentials,
    IAiChatCompletionClientFactory clientFactory,
    IUserPreferencesReader preferences,
    IEffectiveTimeZoneResolver timeZones,
    IEnumerable<IAssistantTool> tools,
    IOptions<AssistantOptions> options,
    ISender sender,
    TimeProvider timeProvider)
    : CommandHandlerBase<InterpretCommand, InterpretResultDto>
{
    private static readonly TimeSpan ConfirmationWindow = TimeSpan.FromMinutes(10);

    /// <summary>The largest voice note the pipeline transcribes: several minutes of Opus voice.</summary>
    public const int MaxAudioBytes = 5 * 1024 * 1024;

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
        var toolContext = AssistantToolContextResolver.For(userId, profile, timeZone);
        var turn = new Turn(conversation, isNewConversation, now, userId, profile);

        var client = clientFactory.GetClient(profile.ChatProvider);
        var start = timeProvider.GetTimestamp();
        long Elapsed() => (long)timeProvider.GetElapsedTime(start).TotalMilliseconds;

        // A voice note is transcribed by its own call first, so what was heard is logged as the
        // utterance and echoed back, and the rest of the pipeline stays identical to typed text. The
        // audio bytes are not retained. Its tokens and latency are folded into the invocation.
        string? transcript = null;
        var transcriptionTokens = new TokenUsage(0, 0);
        if (input.Audio is { } audio)
        {
            if (audio.Data.Length > MaxAudioBytes)
                return await RecordAsync(turn, VoiceNotePlaceholder, [new Step(InvocationStatus.Clarification, Result: toolContext.Text(
                    "Esse áudio é longo demais. Mande um de até alguns minutos.",
                    "That voice note is too long. Keep it to a few minutes."))],
                    latencyMs: 0, new TokenUsage(0, 0), transcript: null, ct);

            try
            {
                var transcription = await client.CompleteAsync(new ChatRequest(
                    profile.ChatModel,
                    [ChatMessage.User(TranscriptionPrompt(toolContext.Locale), audio)],
                    Temperature: 0,
                    ApiKey: keyResult.Value), ct);
                transcript = transcription.Message.Content?.Trim();
                transcriptionTokens = transcription.Usage;
            }
            catch (AiException ex)
            {
                return await RecordAsync(turn, VoiceNotePlaceholder, [new Step(InvocationStatus.ProviderError, Error: ex.Message)],
                    Elapsed(), new TokenUsage(0, 0), transcript: null, ct);
            }

            if (string.IsNullOrEmpty(transcript))
                return await RecordAsync(turn, VoiceNotePlaceholder, [new Step(InvocationStatus.Clarification, Result: toolContext.Text(
                    "Não consegui entender o áudio. Pode tentar de novo?",
                    "I couldn't make out the voice note. Could you try again?"))],
                    Elapsed(), transcriptionTokens, transcript: null, ct);

            text = transcript;
        }

        // Non-empty from here on: either typed text or a transcript (checked above).
        var utterance = text!;

        // A "sim" / "não" to a call held in this conversation is settled here: asked again, the model only
        // repeats the call ("Excluir…?") instead of confirming it, and a "não" would leave it hanging.
        if (!isNewConversation && ConfirmationReply.Parse(utterance) is { } yes)
        {
            var held = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
                await context.AcquireRepository<ICommandInvocationRepository>()
                    .ListAwaitingConfirmationAsync(conversation.Id, now, token),
                cancellationToken: ct);
            if (held.Count > 0)
                return await AnswerHeldAsync(turn, utterance, held, yes, transcript, ct);
        }

        var toolsByName = tools.ToDictionary(t => t.Descriptor.Name, StringComparer.Ordinal);
        var descriptors = toolsByName.Values.Select(t => t.Descriptor).ToList();
        var toolDefinitions = descriptors
            .Select(d => new ToolDefinition(d.Name, d.Description, d.ParametersJsonSchema))
            .ToList();

        var systemPrompt = AssistantSystemPrompt.Build(
            localNow, timeZone.Id, prefs?.WeekStartsOn ?? DayOfWeek.Monday, toolContext.Locale, descriptors);

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
            return await RecordAsync(turn, utterance, [new Step(InvocationStatus.ProviderError, Error: ex.Message)],
                Elapsed(), new TokenUsage(0, 0), transcript, ct);
        }

        var usage = new TokenUsage(
            completion.Usage.PromptTokens + transcriptionTokens.PromptTokens,
            completion.Usage.CompletionTokens + transcriptionTokens.CompletionTokens);

        // Now and then the model answers a follow-up by imitating a recap from the history ("[list_tasks: …]")
        // instead of calling the tool. Ask once more without the history: the sentence alone is clear enough.
        if (completion.ToolCalls.Count == 0 && history.Count > 0 && LooksLikeRecap(completion.Message.Content))
        {
            try
            {
                completion = await client.CompleteAsync(chatRequest with { Messages = [messages[0], messages[^1]] }, ct);
            }
            catch (AiException ex)
            {
                return await RecordAsync(turn, utterance, [new Step(InvocationStatus.ProviderError, Error: ex.Message)],
                    Elapsed(), usage, transcript, ct);
            }
            usage = new TokenUsage(
                usage.PromptTokens + completion.Usage.PromptTokens,
                usage.CompletionTokens + completion.Usage.CompletionTokens);
        }

        var latency = Elapsed();

        // The model replied in prose — it is asking a question or declining. Nothing runs.
        if (completion.ToolCalls.Count == 0)
        {
            var message = string.IsNullOrWhiteSpace(completion.Message.Content) || LooksLikeRecap(completion.Message.Content)
                ? toolContext.Text("Não entendi. Pode reformular?", "I didn't understand. Could you rephrase?")
                : completion.Message.Content!;
            return await RecordAsync(turn, utterance, [new Step(InvocationStatus.Clarification, Result: message)],
                latency, usage, transcript, ct);
        }

        // One sentence can ask for several things ("lembra X e cria Y"): every tool call runs (or is held)
        // on its own, in order, and gets its own invocation.
        var listing = ReadListing(conversation.LastListingJson);
        var steps = new List<Step>(completion.ToolCalls.Count);
        foreach (var toolCall in completion.ToolCalls)
            steps.Add(await RunAsync(toolCall, toolsByName, profile.ConfirmationLevel, toolContext, listing, now, ct));

        return await RecordAsync(turn, utterance, steps, latency, usage, transcript, ct);
    }

    /// <summary>
    /// Settles one tool call: rejected when the catalog lacks the tool or the arguments cannot be read,
    /// held (with a readable question) when the command's shifted policy asks for confirmation, otherwise
    /// executed and recorded with the command's real outcome. A <c>ref</c> to a listed item is pinned into
    /// the arguments first (<see cref="ListedRefs.Pin"/>), so a held call keeps pointing at that item.
    /// </summary>
    private static async Task<Step> RunAsync(
        ToolCall toolCall,
        IReadOnlyDictionary<string, IAssistantTool> toolsByName,
        ConfirmationLevel level,
        AssistantToolContext context,
        IReadOnlyList<ListedItem> listing,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var arguments = ListedRefs.Pin(toolCall.Arguments, listing);
        var argumentsJson = arguments.GetRawText();

        // The model named a tool the catalog does not have.
        if (!toolsByName.TryGetValue(toolCall.Name, out var tool))
            return new Step(InvocationStatus.Rejected, toolCall.Name, argumentsJson, Error: context.Text(
                $"Não conheço o comando '{toolCall.Name}'.", $"Unknown command '{toolCall.Name}'."));

        // A number that is not on the last list is refused now — not asked about ("Excluir \"#40\"?") first.
        if (ListedRefs.Unpinned(arguments) is { } missing)
            return new Step(InvocationStatus.Rejected, toolCall.Name, argumentsJson, Error: ListedRefs.NotListed(context, missing));

        try
        {
            if (RequiresConfirmation(tool.Descriptor.Confirmation, level))
            {
                // Find the item before asking about it: no "Excluir…?" for something that is not there, and
                // the question — and the confirmed call — name the real item.
                if (tool is IAssistantTargetedTool targeted)
                {
                    var (target, problem) = await targeted.FindTargetAsync(context, arguments, ct);
                    if (target is null)
                        return new Step(InvocationStatus.Failed, toolCall.Name, argumentsJson, Error: problem);
                    arguments = ListedRefs.PinTarget(arguments, target);
                    argumentsJson = arguments.GetRawText();
                }

                return new Step(InvocationStatus.PendingConfirmation, toolCall.Name, argumentsJson,
                    Result: tool.Describe(context, arguments), ExpiresAt: now + ConfirmationWindow);
            }

            var outcome = await tool.ExecuteAsync(context, arguments, ct);
            return outcome.Success
                ? new Step(InvocationStatus.Executed, toolCall.Name, argumentsJson, Result: outcome.Message,
                    Recap: outcome.Recap, Listed: outcome.Listed)
                : new Step(InvocationStatus.Failed, toolCall.Name, argumentsJson, Error: outcome.Message);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            // Malformed or missing arguments that slipped past the schema — a write-time rejection.
            return new Step(InvocationStatus.Rejected, toolCall.Name, argumentsJson, Error: ex.Message);
        }
    }

    /// <summary>
    /// Confirms (or cancels) every call held in the conversation, through the same commands the buttons use,
    /// and records the exchange so the conversation reads on.
    /// </summary>
    private async Task<Result<InterpretResultDto>> AnswerHeldAsync(
        Turn turn, string utterance, IReadOnlyList<CommandInvocation> held, bool yes, string? transcript, CancellationToken ct)
    {
        var settled = new List<InvocationResultDto>(held.Count);
        foreach (var invocation in held)
        {
            var result = yes
                ? await sender.Send(new ConfirmInvocationCommand(new ConfirmInvocationInput(turn.UserId, invocation.Id)), ct)
                : await sender.Send(new CancelInvocationCommand(new CancelInvocationInput(turn.UserId, invocation.Id)), ct);
            if (result.IsSuccess)
                settled.Add(result.Value!);
        }

        var reply = settled.Count == 1
            ? settled[0].Message
            : string.Join("\n", settled.Select((s, i) => $"{i + 1}. {s.Message}"));

        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            turn.Conversation.Touch(turn.Now);
            await context.AcquireRepository<IConversationRepository>().UpdateAsync(turn.Conversation, token);
            var messages = context.AcquireRepository<IMessageRepository>();
            await messages.AddAsync(Message.Create(turn.Conversation.Id, MessageAuthor.User, utterance, timeProvider), token);
            await messages.AddAsync(Message.Create(turn.Conversation.Id, MessageAuthor.Assistant, reply, timeProvider), token);
            return true;
        }, cancellationToken: ct);

        return Ok(new InterpretResultDto(turn.Conversation.Id, reply, settled, transcript));
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

    /// <summary>
    /// Persists the turn: the conversation touch, the user's utterance, the assistant's combined reply and
    /// one invocation per step, in one unit of work. The provider cost (latency, tokens) is one call, so it
    /// is recorded on the first invocation only. With several steps the reply numbers them, so a caller
    /// can match each confirmation to its line.
    /// </summary>
    private async Task<Result<InterpretResultDto>> RecordAsync(
        Turn turn, string utterance, IReadOnlyList<Step> steps, long latencyMs, TokenUsage usage,
        string? transcript, CancellationToken ct)
    {
        var reply = Combine(steps, s => s.Reply);
        // What the model sees of this reply on the next turn (see AssistantCommandOutcome.Recap): never a
        // list's content, only how its numbers can be pointed at.
        var recap = Combine(steps, s => s.Listed is { Count: > 0 } listed
            ? $"{s.Recap ?? s.Reply} {ListedRefs.Summary(listed)}"
            : s.Recap ?? s.Reply);

        // ponytail: two lists in one turn — the numbers refer to the last one; merge them if that ever bites.
        if (steps.LastOrDefault(s => s.Listed is { Count: > 0 })?.Listed is { } shown)
            turn.Conversation.ShowListing(JsonSerializer.Serialize(shown, JsonSerializerOptions.Web));

        var invocations = steps.Select((step, i) => CommandInvocation.Create(
            turn.UserId, turn.Conversation.Id, utterance, step.CommandName, step.ArgumentsJson,
            step.Status, step.Result, step.Error,
            turn.Profile.ChatProvider, turn.Profile.ChatModel,
            i == 0 ? latencyMs : 0, i == 0 ? usage.PromptTokens : 0, i == 0 ? usage.CompletionTokens : 0,
            step.ExpiresAt, timeProvider)).ToList();

        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var conversations = context.AcquireRepository<IConversationRepository>();
            var messages = context.AcquireRepository<IMessageRepository>();
            var repository = context.AcquireRepository<ICommandInvocationRepository>();

            turn.Conversation.Touch(turn.Now);
            if (turn.IsNewConversation)
                await conversations.AddAsync(turn.Conversation, token);
            else
                await conversations.UpdateAsync(turn.Conversation, token);

            await messages.AddAsync(
                Message.Create(turn.Conversation.Id, MessageAuthor.User, utterance, timeProvider), token);
            await messages.AddAsync(
                Message.Create(turn.Conversation.Id, MessageAuthor.Assistant, recap, timeProvider), token);

            foreach (var invocation in invocations)
                await repository.AddAsync(invocation, token);
            return true;
        }, cancellationToken: ct);

        return Ok(new InterpretResultDto(
            turn.Conversation.Id,
            reply,
            invocations.Select((invocation, i) => new InvocationResultDto(
                invocation.Id, invocation.Status.Value, invocation.CommandName, invocation.ArgumentsJson,
                steps[i].Reply)).ToList(),
            transcript));
    }

    private static string Combine(IReadOnlyList<Step> steps, Func<Step, string> text) =>
        steps.Count == 1
            ? text(steps[0])
            : string.Join("\n", steps.Select((step, i) => $"{i + 1}. {text(step)}"));

    /// <summary>
    /// True when the command must be confirmed before running, once the level shifts its policy.
    /// <see cref="ConfirmationPolicy.Required"/> is not shifted: a deletion is always confirmed.
    /// </summary>
    private static bool RequiresConfirmation(ConfirmationPolicy policy, ConfirmationLevel level) =>
        policy == ConfirmationPolicy.Required || Shift(policy, level) == ConfirmationPolicy.Always;

    /// <summary>A recap line such as "[list_tasks: 3 task(s) shown…]" — what the history keeps, never a reply.</summary>
    private static bool LooksLikeRecap(string? text) =>
        text is not null && RecapPattern().IsMatch(text);

    [GeneratedRegex(@"^\s*\[[a-z_]+:")]
    private static partial Regex RecapPattern();

    /// <summary>The conversation's last list, oldest line first; empty when none was shown (or it is unreadable).</summary>
    private static IReadOnlyList<ListedItem> ReadListing(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<ListedItem>>(json, JsonSerializerOptions.Web) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

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

    /// <summary>What every invocation of one interpretation shares.</summary>
    private sealed record Turn(
        Conversation Conversation, bool IsNewConversation, DateTimeOffset Now, Guid UserId, AssistantProfile Profile);

    /// <summary>How one tool call (or the lack of one) ended, before it is persisted.</summary>
    private sealed record Step(
        InvocationStatus Status,
        string? CommandName = null,
        string? ArgumentsJson = null,
        string? Result = null,
        string? Error = null,
        DateTimeOffset? ExpiresAt = null,
        string? Recap = null,
        IReadOnlyList<ListedItem>? Listed = null)
    {
        public string Reply => Result ?? Error ?? string.Empty;
    }
}
