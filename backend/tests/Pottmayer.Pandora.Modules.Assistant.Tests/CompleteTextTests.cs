using Pottmayer.Pandora.Modules.Assistant.Application.Commands.CompleteText;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class CompleteTextTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly CompleteTextCommand Request = new(new CompleteTextInput(User, "Improve", "Material"));

    private static CompleteTextCommandHandler Handler(FakeAiChatCompletionClient client, bool? enabled = true, bool key = true,
        IAiChatCompletionClientFactory? clientFactory = null)
    {
        var context = new FakeDataContext();
        context.Register<IAssistantProfileRepository>(enabled is null ? new FakeAssistantProfileRepository() :
            new FakeAssistantProfileRepository(AssistantProfile.Create(User, "gemini", Account, "chosen-model", enabled.Value,
                null, ConfirmationLevel.Balanced, TimeProvider.System)));
        return new CompleteTextCommandHandler(new FakeUnitOfWorkFactory(context),
            key ? FakeExternalCredentialProvider.WithKey("user-key") : FakeExternalCredentialProvider.WithoutKey(),
            clientFactory ?? new FakeAiChatCompletionClientFactory(client));
    }

    [Fact]
    public async Task Uses_the_user_model_and_key_without_tools_or_conversation_history()
    {
        var client = FakeAiChatCompletionClient.Replies("  Improved  ");
        var result = await Handler(client).Handle(Request, default);
        Assert.Equal("Improved", result.Value);
        Assert.Equal("chosen-model", client.LastRequest!.Model);
        Assert.Equal("user-key", client.LastRequest.ApiKey);
        Assert.Null(client.LastRequest.Tools);
        Assert.Equal(["Improve", "Material"], client.LastRequest.Messages.Select(m => m.Content));
    }

    [Theory]
    [InlineData(null, true, "Assistant.NotEnabled")]
    [InlineData(false, true, "Assistant.NotEnabled")]
    [InlineData(true, false, "Assistant.NoApiKey")]
    public async Task Unconfigured_profiles_never_call_the_provider(bool? enabled, bool key, string error)
    {
        var client = FakeAiChatCompletionClient.Replies("unused");
        var result = await Handler(client, enabled, key).Handle(Request, default);
        Assert.Equal(error, Assert.Single(result.Errors).Code);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Provider_failures_do_not_expose_the_provider_message()
    {
        var client = FakeAiChatCompletionClient.Throws(new AiException("gemini", "secret provider details", isPermanent: false));
        var result = await Handler(client).Handle(Request, default);
        Assert.Equal("Assistant.TextCompletionFailed", Assert.Single(result.Errors).Code);
        Assert.DoesNotContain("secret", result.Errors[0].Message);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Empty_completions_are_friendly_errors(string? text)
    {
        var result = await Handler(FakeAiChatCompletionClient.Replies(text!)).Handle(Request, default);
        Assert.Equal("Assistant.TextCompletionFailed", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Oversized_completions_are_not_returned_to_the_editor()
    {
        var result = await Handler(FakeAiChatCompletionClient.Replies(new string('x', 16_001))).Handle(Request, default);
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Timeouts_and_network_failures_are_friendly_errors(bool timeout)
    {
        var exception = timeout ? (Exception)new OperationCanceledException() : new HttpRequestException("details");
        var handler = Handler(FakeAiChatCompletionClient.Replies("unused"), clientFactory: new ThrowingClient(exception));
        var result = await handler.Handle(Request, default);
        Assert.Equal("Assistant.TextCompletionFailed", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = Handler(FakeAiChatCompletionClient.Replies("unused"), clientFactory: new ThrowingClient(new OperationCanceledException(cancellation.Token)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(Request, cancellation.Token).AsTask());
    }

    private sealed class ThrowingClient(Exception exception) : IAiChatCompletionClientFactory, IAiChatCompletionClient
    {
        public IAiChatCompletionClient GetClient(string provider) => this;
        public Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw exception;
    }
}
