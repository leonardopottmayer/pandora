using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Tars.Ai.Abstractions;
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
    {
        var invocations = new FakeCommandInvocationRepository();
        var context = new FakeDataContext();
        context.Register<IAssistantProfileRepository>(profile is not null
            ? new FakeAssistantProfileRepository(profile)
            : new FakeAssistantProfileRepository());
        context.Register<IConversationRepository>(new FakeConversationRepository());
        context.Register<IMessageRepository>(new FakeMessageRepository());
        context.Register<ICommandInvocationRepository>(invocations);

        var handler = new InterpretCommandHandler(
            new FakeUnitOfWorkFactory(context),
            credentials,
            new FakeAiChatCompletionClientFactory(client),
            FakeUserPreferencesReader.With("America/Sao_Paulo"),
            tools,
            TimeProvider.System);

        return (handler, invocations);
    }

    private static InterpretCommand Sentence(string text = "me lembra de pagar o aluguel amanhã às 10")
        => new(new InterpretInput(User, text));

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
        Assert.Equal(InvocationStatus.Clarification.Value, result.Value!.Status);
        Assert.Equal("Para quando é o lembrete?", result.Value.Message);
        Assert.Null(result.Value.CommandName);
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
        Assert.Equal(InvocationStatus.Executed.Value, result.Value!.Status);
        Assert.Equal("create_reminder", result.Value.CommandName);
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

        Assert.Equal(InvocationStatus.PendingConfirmation.Value, result.Value!.Status);
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

        Assert.Equal(InvocationStatus.Rejected.Value, result.Value!.Status);
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

        Assert.Equal(InvocationStatus.Rejected.Value, result.Value!.Status);
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

        Assert.Equal(InvocationStatus.Failed.Value, result.Value!.Status);
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
        Assert.Equal(InvocationStatus.ProviderError.Value, result.Value!.Status);
        var invocation = Assert.Single(invocations.Added);
        Assert.Equal(InvocationStatus.ProviderError, invocation.Status);
        Assert.Equal("endpoint down", invocation.Error);
    }
}
