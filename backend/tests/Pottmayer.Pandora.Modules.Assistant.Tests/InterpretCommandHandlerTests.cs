using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class InterpretCommandHandlerTests
{
    private static readonly Guid User = Guid.NewGuid();

    private static AssistantProfile EnabledProfile(bool enabled = true, ConfirmationLevel? level = null) =>
        AssistantProfile.Create(User, "gemini", "gemini-3.6-flash", enabled, null,
            level ?? ConfirmationLevel.Balanced, TimeProvider.System);

    private static (InterpretCommandHandler Handler, FakeCommandInvocationRepository Invocations) Build(
        FakeAiChatCompletionClient client,
        FakeExternalCredentialProvider credentials,
        AssistantProfile profile,
        params IAssistantTool[] tools)
        => Build(client, credentials, profile, new FakeConversationRepository(), new FakeMessageRepository(), tools);

    private static (InterpretCommandHandler Handler, FakeCommandInvocationRepository Invocations) Build(
        FakeAiChatCompletionClient client,
        FakeExternalCredentialProvider credentials,
        AssistantProfile profile,
        FakeConversationRepository conversations,
        FakeMessageRepository messages,
        IAssistantTool[] tools,
        FakeCommandInvocationRepository? invocationRepository = null,
        FakeSender? sender = null)
    {
        var invocations = invocationRepository ?? new FakeCommandInvocationRepository();
        var context = new FakeDataContext();
        context.Register<IAssistantProfileRepository>(profile is not null
            ? new FakeAssistantProfileRepository(profile)
            : new FakeAssistantProfileRepository());
        context.Register<IConversationRepository>(conversations);
        context.Register<IMessageRepository>(messages);
        context.Register<ICommandInvocationRepository>(invocations);

        var handler = new InterpretCommandHandler(
            new FakeUnitOfWorkFactory(context),
            credentials,
            new FakeAiChatCompletionClientFactory(client),
            FakeUserPreferencesReader.With("America/Sao_Paulo"),
            FakeEffectiveTimeZoneResolver.With("America/Sao_Paulo"),
            tools,
            Microsoft.Extensions.Options.Options.Create(new AssistantOptions()),
            sender ?? new FakeSender(),
            TimeProvider.System);

        return (handler, invocations);
    }

    private static InterpretCommand Sentence(string text = "me lembra de pagar o aluguel amanhã às 10")
        => new(new InterpretInput(User, text));

    [Fact]
    public async Task Resends_recent_conversation_history_as_context_for_a_follow_up()
    {
        var conversation = Conversation.Start(User, TimeProvider.System);
        var messages = new FakeMessageRepository(
            Message.Create(conversation.Id, MessageAuthor.User, "me lembra de pagar a conta", TimeProvider.System),
            Message.Create(conversation.Id, MessageAuthor.Assistant, "Quando?", TimeProvider.System));
        var client = FakeAiChatCompletionClient.Replies("ok");

        var (handler, _) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(conversation), messages, []);

        await handler.Handle(Sentence("amanhã às 10"), CancellationToken.None);

        // [system, prior user, prior assistant, current user] — the follow-up carries its context.
        var sent = client.LastRequest!.Messages;
        Assert.Equal(4, sent.Count);
        Assert.Equal(ChatRole.System, sent[0].Role);
        Assert.Equal(ChatRole.User, sent[1].Role);
        Assert.Equal("me lembra de pagar a conta", sent[1].Content);
        Assert.Equal(ChatRole.Assistant, sent[2].Role);
        Assert.Equal("Quando?", sent[2].Content);
        Assert.Equal(ChatRole.User, sent[3].Role);
        Assert.Equal("amanhã às 10", sent[3].Content);
    }

    [Fact]
    public async Task A_new_conversation_sends_no_history()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile());

        await handler.Handle(Sentence("primeira mensagem"), CancellationToken.None);

        // Just [system, user] — nothing to recall on a fresh thread.
        Assert.Equal(2, client.LastRequest!.Messages.Count);
        Assert.Equal(ChatRole.System, client.LastRequest.Messages[0].Role);
        Assert.Equal(ChatRole.User, client.LastRequest.Messages[1].Role);
    }

    [Fact]
    public async Task A_read_reply_reaches_the_user_but_the_history_keeps_only_its_recap()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("list_agenda", """{ "from": "2026-09-05" }""");
        var messages = new FakeMessageRepository();
        var (handler, _) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(), messages,
            [FakeAssistantTool.Reads("list_agenda", "09:00 Dentista", "[list_agenda: 1 item]")]);

        var result = await handler.Handle(Sentence("o que tenho amanhã?"), CancellationToken.None);

        Assert.Equal("09:00 Dentista", result.Value!.Message);
        Assert.Equal("[list_agenda: 1 item]", messages.Added.Single(m => m.Author == MessageAuthor.Assistant).Content);
    }

    [Fact]
    public async Task A_numbered_list_is_kept_on_the_conversation_and_the_history_learns_only_its_kinds()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("list_agenda", """{ "from": "2026-09-05" }""");
        var conversations = new FakeConversationRepository();
        var messages = new FakeMessageRepository();
        var (handler, _) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), conversations, messages,
            [FakeAssistantTool.Lists("list_agenda", "1. Dentista\n2. Pagar luz", "[list_agenda: 2 item(s)]",
                new ListedItem("event", Guid.NewGuid(), "Dentista"), new ListedItem("task", Guid.NewGuid(), "Pagar luz"))]);

        await handler.Handle(Sentence("o que tenho amanhã?"), CancellationToken.None);

        var recap = messages.Added.Single(m => m.Author == MessageAuthor.Assistant).Content;
        Assert.StartsWith("[list_agenda: 2 item(s)] [numbered for reference: 1=event, 2=task;", recap);
        Assert.DoesNotContain("Dentista", recap);
        Assert.Contains("Pagar luz", Assert.Single(conversations.Added).LastListingJson);
    }

    [Fact]
    public async Task A_ref_is_pinned_to_the_listed_item_before_the_call_is_held_so_confirming_acts_on_it()
    {
        var conversation = Conversation.Start(User, TimeProvider.System);
        var taskId = Guid.NewGuid();
        conversation.ShowListing(System.Text.Json.JsonSerializer.Serialize(
            new[] { new ListedItem("event", Guid.NewGuid(), "Dentista"), new ListedItem("task", taskId, "Pagar luz") },
            System.Text.Json.JsonSerializerOptions.Web));
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("delete_task", """{ "ref": 2, "ref_id": "forged" }""");
        var (handler, invocations) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(conversation), new FakeMessageRepository(),
            [FakeAssistantTool.Required("delete_task")]);

        await handler.Handle(Sentence("exclui o 2"), CancellationToken.None);

        using var stored = System.Text.Json.JsonDocument.Parse(Assert.Single(invocations.Added).ArgumentsJson!);
        var context = new AssistantToolContext(User, "pt-BR", TimeZoneInfo.Utc);
        var pinned = ToolArguments.OptionalRef(context, stored.RootElement, "task");
        Assert.Equal((taskId, "Pagar luz"), (pinned!.Id, pinned.Label));
        Assert.Throws<ArgumentException>(() => ToolArguments.OptionalRef(context, stored.RootElement, "event"));
    }

    [Fact]
    public async Task A_ref_outside_the_last_list_is_rejected_in_the_users_language()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("complete_task", """{ "ref": 3 }""");
        var tool = new RefReadingTool("complete_task");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), tool);

        var result = await handler.Handle(Sentence("conclui o 3"), CancellationToken.None);

        Assert.Equal("Não há item 3 na última lista. Peça a lista de novo.", result.Value!.Message);
        Assert.Equal(InvocationStatus.Rejected.Value, result.Value.Invocations[0].Status);
    }

    [Fact]
    public async Task A_reply_imitating_a_recap_is_asked_again_without_the_history()
    {
        var conversation = Conversation.Start(User, TimeProvider.System);
        var messages = new FakeMessageRepository(
            Message.Create(conversation.Id, MessageAuthor.User, "minhas tarefas", TimeProvider.System),
            Message.Create(conversation.Id, MessageAuthor.Assistant, "[list_tasks: 3 task(s) shown to the user]", TimeProvider.System));
        var client = FakeAiChatCompletionClient.Script(
            new ChatMessage(ChatRole.Assistant, "[list_tasks: 0 task(s) shown to the user; content withheld from you]"),
            new ChatMessage(ChatRole.Assistant, null, [Call("list_tasks")]));
        var tool = FakeAssistantTool.Succeeds("list_tasks", "1. Pagar luz");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(conversation), messages, [tool]);

        var result = await handler.Handle(Sentence("minhas tarefas"), CancellationToken.None);

        Assert.Equal("1. Pagar luz", result.Value!.Message);
        Assert.Equal([4, 2], client.Requests.Select(r => r.Messages.Count)); // the retry left the history out
    }

    [Theory]
    [InlineData("sim", true)]
    [InlineData("Pode, sim!", true)]
    [InlineData("não, deixa", false)]
    [InlineData("deixa pra lá", false)]
    public async Task A_typed_yes_or_no_settles_the_held_call_without_asking_the_model(string reply, bool confirms)
    {
        var conversation = Conversation.Start(User, TimeProvider.System);
        var invocations = new FakeCommandInvocationRepository();
        var held = CommandInvocation.Create(User, conversation.Id, "exclui a tarefa x", "delete_task", "{}",
            InvocationStatus.PendingConfirmation, "Excluir a tarefa \"x\"?", null, "gemini", "m", 0, 0, 0,
            DateTimeOffset.UtcNow.AddMinutes(5), TimeProvider.System);
        await invocations.AddAsync(held);
        var sender = new FakeSender
        {
            Response = Result<Application.Dtos.InvocationResultDto>.Success(
                new(held.Id, "executed", "delete_task", "{}", "Tarefa \"x\" excluída.")),
        };
        var client = FakeAiChatCompletionClient.Replies("should not be asked");
        var messages = new FakeMessageRepository();
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(conversation), messages, [], invocations, sender);

        var result = await handler.Handle(Sentence(reply), CancellationToken.None);

        Assert.Equal(0, client.Calls);
        var sent = Assert.Single(sender.Sent);
        Assert.Equal(confirms ? typeof(Application.Commands.ConfirmInvocation.ConfirmInvocationCommand)
                              : typeof(Application.Commands.CancelInvocation.CancelInvocationCommand), sent.GetType());
        Assert.Equal("Tarefa \"x\" excluída.", result.Value!.Message);
        Assert.Equal("[delete_task: executed; reply shown to the user, content withheld from you]",
            messages.Added.Single(m => m.Author == MessageAuthor.Assistant).Content);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_tool_reply_naming_the_users_items_stays_out_of_the_history(bool succeeds)
    {
        // "Pagar luz" came from the database (a pinned number, an ambiguous match), not from the sentence.
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("complete_task", """{ "title": "pagar" }""");
        var messages = new FakeMessageRepository();
        var (handler, _) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(), messages,
            [succeeds
                ? FakeAssistantTool.Succeeds("complete_task", "Tarefa \"Pagar luz\" concluída.")
                : FakeAssistantTool.Fails("complete_task", "\"pagar\" bate com mais de um item: \"Pagar luz\", \"Pagar água\".")]);

        var result = await handler.Handle(Sentence("conclui pagar"), CancellationToken.None);

        Assert.Contains("Pagar luz", result.Value!.Message);
        Assert.Equal($"[complete_task: {(succeeds ? "executed" : "failed")}; reply shown to the user, content withheld from you]",
            messages.Added.Single(m => m.Author == MessageAuthor.Assistant).Content);
    }

    [Fact]
    public async Task A_clarification_is_kept_in_the_history_as_the_model_said_it()
    {
        var client = FakeAiChatCompletionClient.Replies("Para que horas?");
        var messages = new FakeMessageRepository();
        var (handler, _) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), new FakeConversationRepository(), messages, []);

        await handler.Handle(Sentence("lembra de ligar pro João"), CancellationToken.None);

        Assert.Equal("Para que horas?", messages.Added.Single(m => m.Author == MessageAuthor.Assistant).Content);
    }

    [Fact]
    public async Task A_no_that_says_more_still_goes_to_the_model()
    {
        var conversation = Conversation.Start(User, TimeProvider.System);
        var invocations = new FakeCommandInvocationRepository();
        await invocations.AddAsync(CommandInvocation.Create(User, conversation.Id, "lembra x", "create_reminder", "{}",
            InvocationStatus.PendingConfirmation, "?", null, "gemini", "m", 0, 0, 0,
            DateTimeOffset.UtcNow.AddMinutes(5), TimeProvider.System));
        var client = FakeAiChatCompletionClient.Replies("Para que horas?");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(),
            new FakeConversationRepository(conversation), new FakeMessageRepository(), [], invocations);

        await handler.Handle(Sentence("não, muda pra 11h"), CancellationToken.None);

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task A_number_off_the_last_list_is_refused_before_any_confirmation_is_asked()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("delete_task", """{ "ref": 40 }""");
        var tool = FakeAssistantTool.Required("delete_task");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), tool);

        var result = await handler.Handle(Sentence("exclui a 40"), CancellationToken.None);

        Assert.Equal(InvocationStatus.Rejected.Value, result.Value!.Invocations[0].Status);
        Assert.Equal("Não há item 40 na última lista. Peça a lista de novo.", result.Value.Message);
    }

    [Fact]
    public async Task A_required_confirmation_holds_the_call_even_for_a_trusting_profile()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("delete_task", """{ "task": "luz" }""");
        var tool = FakeAssistantTool.Required("delete_task");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"),
            EnabledProfile(level: ConfirmationLevel.Trusting), tool);

        var result = await handler.Handle(Sentence("exclui a tarefa luz"), CancellationToken.None);

        Assert.Equal(InvocationStatus.PendingConfirmation.Value, result.Value!.Invocations[0].Status);
        Assert.Equal(0, tool.Calls);
    }

    /// <summary>A tool that resolves its target through <see cref="ToolArguments.OptionalRef"/>, like the real ones.</summary>
    private sealed class RefReadingTool(string name) : IAssistantTool
    {
        public AssistantCommandDescriptor Descriptor { get; } =
            new(name, "x", """{ "type": "object" }""", ConfirmationPolicy.Never, []);

        public string Describe(AssistantToolContext context, System.Text.Json.JsonElement arguments) => "?";

        public Task<AssistantCommandOutcome> ExecuteAsync(
            AssistantToolContext context, System.Text.Json.JsonElement arguments, CancellationToken ct = default) =>
            Task.FromResult(AssistantCommandOutcome.Ok(ToolArguments.OptionalRef(context, arguments, "task")!.Label));
    }

    [Fact]
    public async Task Fails_when_the_assistant_is_not_enabled()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(enabled: false));

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.Calls);
        Assert.Empty(invocations.Added);
    }

    [Fact]
    public async Task Fails_when_no_api_key_is_configured()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithoutKey(), EnabledProfile());

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.Calls);
        Assert.Empty(invocations.Added);
    }

    [Fact]
    public async Task A_prose_reply_is_recorded_as_a_clarification_and_runs_nothing()
    {
        var client = FakeAiChatCompletionClient.Replies("Para quando é o lembrete?");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvocationStatus.Clarification.Value, result.Value!.Invocations[0].Status);
        Assert.Equal("Para quando é o lembrete?", result.Value.Message);
        Assert.Null(result.Value.Invocations[0].CommandName);
        Assert.Equal(0, command.Calls);
        var invocation = Assert.Single(invocations.Added);
        Assert.Equal(InvocationStatus.Clarification, invocation.Status);
    }

    [Fact]
    public async Task A_tool_call_runs_the_matching_command_and_records_it_as_executed()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall(
            "create_reminder", """{ "title": "Aluguel", "remindAt": "2026-09-05T10:00:00-03:00" }""",
            promptTokens: 11, completionTokens: 7);
        var command = FakeAssistantTool.Succeeds("create_reminder", "Lembrete criado.");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvocationStatus.Executed.Value, result.Value!.Invocations[0].Status);
        Assert.Equal("create_reminder", result.Value.Invocations[0].CommandName);
        Assert.Equal("Lembrete criado.", result.Value.Message);
        Assert.Equal(1, command.Calls);
        Assert.Equal("Aluguel", command.LastArguments!.Value.GetProperty("title").GetString());

        var invocation = Assert.Single(invocations.Added);
        Assert.Equal(InvocationStatus.Executed, invocation.Status);
        Assert.Equal("gemini", invocation.Provider);
        Assert.Equal("gemini-3.6-flash", invocation.Model);
        Assert.Equal(11, invocation.PromptTokens);
        Assert.Equal(7, invocation.CompletionTokens);
        Assert.NotNull(invocation.ArgumentsJson);
    }

    [Fact]
    public async Task Sends_the_catalog_tools_and_the_users_key_and_model_to_the_provider()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("secret"), EnabledProfile(), command);

        await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal("secret", client.LastRequest!.ApiKey);
        Assert.Equal("gemini-3.6-flash", client.LastRequest.Model);
        Assert.Equal(0d, client.LastRequest.Temperature);
        Assert.Contains(client.LastRequest.Tools!, t => t.Name == "create_reminder");
    }

    [Fact]
    public async Task A_strict_profile_holds_the_tool_call_for_confirmation_instead_of_running_it()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall(
            "create_reminder", """{ "title": "Aluguel", "remindAt": "2026-09-05T10:00:00-03:00" }""");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, invocations) = Build(
            client, FakeExternalCredentialProvider.WithKey("k"),
            EnabledProfile(level: ConfirmationLevel.Strict), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal(InvocationStatus.PendingConfirmation.Value, result.Value!.Invocations[0].Status);
        Assert.Equal(0, command.Calls); // held, not executed
        var invocation = Assert.Single(invocations.Added);
        Assert.Equal(InvocationStatus.PendingConfirmation, invocation.Status);
        Assert.NotNull(invocation.ExpiresAt);
    }

    [Fact]
    public async Task An_unknown_tool_is_recorded_as_rejected()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("delete_everything", "{}");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal(InvocationStatus.Rejected.Value, result.Value!.Invocations[0].Status);
        Assert.Equal(0, command.Calls);
        Assert.Equal(InvocationStatus.Rejected, Assert.Single(invocations.Added).Status);
    }

    [Fact]
    public async Task Malformed_arguments_are_recorded_as_rejected()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("create_reminder", "{}");
        var command = FakeAssistantTool.Throws("create_reminder", new ArgumentException("title obrigatório"));
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal(InvocationStatus.Rejected.Value, result.Value!.Invocations[0].Status);
        var invocation = Assert.Single(invocations.Added);
        Assert.Equal(InvocationStatus.Rejected, invocation.Status);
        Assert.Equal("title obrigatório", invocation.Error);
    }

    [Fact]
    public async Task A_command_failure_is_recorded_as_failed()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall(
            "create_reminder", """{ "title": "x", "remindAt": "2026-09-05T10:00:00-03:00" }""");
        var command = FakeAssistantTool.Fails("create_reminder", "O título é obrigatório.");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal(InvocationStatus.Failed.Value, result.Value!.Invocations[0].Status);
        Assert.Equal("O título é obrigatório.", result.Value.Message);
        Assert.Equal(InvocationStatus.Failed, Assert.Single(invocations.Added).Status);
    }

    [Fact]
    public async Task A_provider_error_is_recorded_as_provider_error()
    {
        var client = FakeAiChatCompletionClient.Throws(new AiException("gemini", "endpoint down", isPermanent: false));
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvocationStatus.ProviderError.Value, result.Value!.Invocations[0].Status);
        var invocation = Assert.Single(invocations.Added);
        Assert.Equal(InvocationStatus.ProviderError, invocation.Status);
        Assert.Equal("endpoint down", invocation.Error);
    }

    private static InterpretCommand VoiceNote() =>
        new(new InterpretInput(User, Text: null, Audio: new ChatAttachment(new byte[] { 1, 2, 3 }, "audio/ogg")));

    [Fact]
    public async Task A_voice_note_is_transcribed_then_interpreted_as_the_transcript()
    {
        var toolCall = new ToolCall("create_reminder", System.Text.Json.JsonDocument.Parse(
            """{ "title": "Pagar o aluguel", "remindAt": "2026-09-05T10:00:00-03:00" }""").RootElement.Clone());
        var client = FakeAiChatCompletionClient.Script(
            new ChatMessage(ChatRole.Assistant, " me lembra de pagar o aluguel amanhã às 10 "),
            new ChatMessage(ChatRole.Assistant, null, [toolCall]));
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(VoiceNote(), CancellationToken.None);

        Assert.Equal(2, client.Calls);
        var transcription = Assert.Single(client.Requests[0].Messages);
        Assert.Equal("audio/ogg", Assert.Single(transcription.Attachments!).MimeType);
        Assert.Null(client.Requests[0].Tools);
        Assert.Equal("me lembra de pagar o aluguel amanhã às 10", client.Requests[1].Messages[^1].Content);

        Assert.Equal(InvocationStatus.Executed.Value, result.Value!.Invocations[0].Status);
        Assert.Equal("me lembra de pagar o aluguel amanhã às 10", result.Value.Transcript);
        var invocation = Assert.Single(invocations.Added);
        Assert.Equal("me lembra de pagar o aluguel amanhã às 10", invocation.Utterance);
        Assert.Equal(4, invocation.PromptTokens); // transcription + interpretation
    }

    [Fact]
    public async Task An_unintelligible_voice_note_asks_again_and_runs_nothing()
    {
        var client = FakeAiChatCompletionClient.Replies("   ");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        var result = await handler.Handle(VoiceNote(), CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Equal(InvocationStatus.Clarification.Value, result.Value!.Invocations[0].Status);
        Assert.Null(result.Value.Transcript);
        Assert.Equal("[voice note]", Assert.Single(invocations.Added).Utterance);
    }

    private static ToolCall Call(string name, string argumentsJson = "{}") =>
        new(name, System.Text.Json.JsonDocument.Parse(argumentsJson).RootElement.Clone());

    [Fact]
    public async Task Every_tool_call_in_the_reply_runs_and_is_recorded_on_its_own()
    {
        var client = FakeAiChatCompletionClient.Script(
            new ChatMessage(ChatRole.Assistant, null, [Call("create_reminder"), Call("create_task")]));
        var reminder = FakeAssistantTool.Succeeds("create_reminder", "Lembrete criado.");
        var task = FakeAssistantTool.Succeeds("create_task", "Tarefa criada.");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), reminder, task);

        var result = await handler.Handle(Sentence("lembra X e cria a tarefa Y"), CancellationToken.None);

        Assert.Equal((1, 1), (reminder.Calls, task.Calls));
        Assert.Equal("1. Lembrete criado.\n2. Tarefa criada.", result.Value!.Message);
        Assert.Equal(["create_reminder", "create_task"], result.Value.Invocations.Select(i => i.CommandName));
        Assert.Equal(2, invocations.Added.Count);
        Assert.Equal(2, invocations.Added[0].PromptTokens); // the one provider call is costed once
        Assert.Equal(0, invocations.Added[1].PromptTokens);
    }

    [Fact]
    public async Task A_held_call_asks_with_the_tools_own_description()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("create_reminder", "{}");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"),
            EnabledProfile(level: ConfirmationLevel.Strict), command);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal("Run create_reminder?", result.Value!.Message);
        Assert.Equal(0, command.Calls);
    }

    [Fact]
    public async Task Tools_run_with_the_users_locale_and_zone()
    {
        var client = FakeAiChatCompletionClient.RepliesWithToolCall("create_reminder", "{}");
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile(), command);

        await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal(User, command.LastContext!.UserId);
        Assert.Equal("pt-BR", command.LastContext.Locale);
        Assert.Equal("America/Sao_Paulo", command.LastContext.TimeZone.Id);
    }

    [Fact]
    public async Task The_pipelines_own_replies_follow_the_locale_override()
    {
        var client = FakeAiChatCompletionClient.Replies("   ");
        var profile = AssistantProfile.Create(User, "gemini", "m", true, "en-US", ConfirmationLevel.Balanced, TimeProvider.System);
        var (handler, _) = Build(client, FakeExternalCredentialProvider.WithKey("k"), profile);

        var result = await handler.Handle(Sentence(), CancellationToken.None);

        Assert.Equal("I didn't understand. Could you rephrase?", result.Value!.Message);
    }

    [Fact]
    public async Task An_oversized_voice_note_is_refused_without_calling_the_provider()
    {
        var client = FakeAiChatCompletionClient.Replies("never");
        var (handler, invocations) = Build(client, FakeExternalCredentialProvider.WithKey("k"), EnabledProfile());

        var result = await handler.Handle(new InterpretCommand(new InterpretInput(User, null,
            Audio: new ChatAttachment(new byte[InterpretCommandHandler.MaxAudioBytes + 1], "audio/ogg"))), CancellationToken.None);

        Assert.Equal(0, client.Calls);
        Assert.Equal(InvocationStatus.Clarification.Value, result.Value!.Invocations[0].Status);
        Assert.Equal("Esse áudio é longo demais. Mande um de até alguns minutos.", result.Value.Message);
        Assert.Equal("[voice note]", Assert.Single(invocations.Added).Utterance);
    }
}
