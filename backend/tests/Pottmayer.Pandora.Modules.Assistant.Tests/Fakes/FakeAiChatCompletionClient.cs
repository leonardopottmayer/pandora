using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>
/// A chat client that either echoes a canned reply or throws a configured <see cref="AiException"/>.
/// Records the last request so a test can assert which model/key the handler sent.
/// </summary>
internal sealed class FakeAiChatCompletionClient : IAiChatCompletionClient
{
    private readonly string? _reply;
    private readonly AiException? _throw;

    private FakeAiChatCompletionClient(string? reply, AiException? toThrow)
    {
        _reply = reply;
        _throw = toThrow;
    }

    public static FakeAiChatCompletionClient Replies(string reply) => new(reply, null);
    public static FakeAiChatCompletionClient Throws(AiException ex) => new(null, ex);

    public ChatRequest? LastRequest { get; private set; }
    public int Calls { get; private set; }

    public Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        Calls++;

        if (_throw is not null)
            throw _throw;

        var completion = new ChatCompletion(
            request.Model,
            new ChatMessage(ChatRole.Assistant, _reply),
            new TokenUsage(0, 0));
        return Task.FromResult(completion);
    }
}

/// <summary>A factory that always hands back the one fake client, whatever provider is asked for.</summary>
internal sealed class FakeAiChatCompletionClientFactory(FakeAiChatCompletionClient client) : IAiChatCompletionClientFactory
{
    public IAiChatCompletionClient GetClient(string provider) => client;
}
