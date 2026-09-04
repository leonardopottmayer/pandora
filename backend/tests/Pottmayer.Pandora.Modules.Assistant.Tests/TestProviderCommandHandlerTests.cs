using Pottmayer.Pandora.Modules.Assistant.Application;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.TestProvider;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Tars.Ai.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class TestProviderCommandHandlerTests
{
    private static readonly Guid User = Guid.NewGuid();

    private static TestProviderCommand Command(string? model = null)
        => new(new TestProviderInput(User, "gemini", model));

    private static TestProviderCommandHandler Build(
        FakeExternalCredentialProvider credentials, FakeAiChatCompletionClient client)
        => new(credentials, new FakeAiChatCompletionClientFactory(client), TimeProvider.System);

    [Fact]
    public async Task Reports_no_key_and_never_calls_the_provider_when_the_key_is_missing()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var handler = Build(FakeExternalCredentialProvider.WithoutKey(), client);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Ok);
        Assert.Equal("no_key", result.Value.ErrorKind);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Reports_ok_with_the_reply_when_the_provider_answers()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var handler = Build(FakeExternalCredentialProvider.WithKey("secret-key"), client);

        var result = await handler.Handle(Command(model: "gemini-3.6-flash"), CancellationToken.None);

        Assert.True(result.Value!.Ok);
        Assert.Equal("ok", result.Value.Reply);
        Assert.Null(result.Value.ErrorKind);
        Assert.True(result.Value.LatencyMs >= 0);
        // The user's key and chosen model reached the provider.
        Assert.Equal("secret-key", client.LastRequest!.ApiKey);
        Assert.Equal("gemini-3.6-flash", client.LastRequest.Model);
    }

    [Fact]
    public async Task Falls_back_to_the_default_model_when_none_is_supplied()
    {
        var client = FakeAiChatCompletionClient.Replies("ok");
        var handler = Build(FakeExternalCredentialProvider.WithKey("k"), client);

        await handler.Handle(Command(model: null), CancellationToken.None);

        Assert.Equal(AssistantDefaults.Model, client.LastRequest!.Model);
    }

    [Fact]
    public async Task Classifies_a_permanent_failure_as_rejected()
    {
        var client = FakeAiChatCompletionClient.Throws(
            new AiException("gemini", "bad key", isPermanent: true));
        var handler = Build(FakeExternalCredentialProvider.WithKey("k"), client);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.False(result.Value!.Ok);
        Assert.Equal("rejected", result.Value.ErrorKind);
        Assert.Equal("bad key", result.Value.Error);
    }

    [Fact]
    public async Task Classifies_a_transient_failure_as_unreachable()
    {
        var client = FakeAiChatCompletionClient.Throws(
            new AiException("gemini", "endpoint down", isPermanent: false));
        var handler = Build(FakeExternalCredentialProvider.WithKey("k"), client);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.False(result.Value!.Ok);
        Assert.Equal("unreachable", result.Value.ErrorKind);
    }
}
