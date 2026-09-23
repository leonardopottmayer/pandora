using System.Text.Json;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>
/// A chat client that returns a canned completion — prose, a tool call, or a thrown
/// <see cref="AiException"/>. Records the last request so a test can assert which model/key/tools the
/// handler sent.
/// </summary>
internal sealed class FakeAiChatCompletionClient : IAiChatCompletionClient
{
    private readonly ChatMessage? _message;
    private readonly TokenUsage _usage;
    private readonly AiException? _throw;
    private readonly Queue<ChatMessage> _script = new();

    private FakeAiChatCompletionClient(ChatMessage? message, TokenUsage usage, AiException? toThrow)
    {
        _message = message;
        _usage = usage;
        _throw = toThrow;
    }

    public static FakeAiChatCompletionClient Replies(string reply) =>
        new(new ChatMessage(ChatRole.Assistant, reply), new TokenUsage(0, 0), null);

    public static FakeAiChatCompletionClient Throws(AiException ex) =>
        new(null, new TokenUsage(0, 0), ex);

    /// <summary>A completion carrying one tool call with the given raw-JSON arguments.</summary>
    public static FakeAiChatCompletionClient RepliesWithToolCall(
        string name, string argumentsJson, int promptTokens = 3, int completionTokens = 5)
    {
        var arguments = JsonDocument.Parse(argumentsJson).RootElement.Clone();
        var message = new ChatMessage(ChatRole.Assistant, null, [new ToolCall(name, arguments)]);
        return new FakeAiChatCompletionClient(message, new TokenUsage(promptTokens, completionTokens), null);
    }

    /// <summary>
    /// Replies with each message in turn (e.g. a transcript, then a tool call); the last one repeats.
    /// </summary>
    public static FakeAiChatCompletionClient Script(params ChatMessage[] replies)
    {
        var client = new FakeAiChatCompletionClient(replies[^1], new TokenUsage(2, 1), null);
        foreach (var reply in replies[..^1])
            client._script.Enqueue(reply);
        return client;
    }

    public List<ChatRequest> Requests { get; } = [];
    public ChatRequest? LastRequest { get; private set; }
    public int Calls { get; private set; }

    public Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        Requests.Add(request);
        Calls++;

        if (_throw is not null)
            throw _throw;

        var message = _script.Count > 0 ? _script.Dequeue() : _message!;
        var completion = new ChatCompletion(request.Model, message, _usage);
        return Task.FromResult(completion);
    }
}

/// <summary>A factory that always hands back the one fake client, whatever provider is asked for.</summary>
internal sealed class FakeAiChatCompletionClientFactory(FakeAiChatCompletionClient client) : IAiChatCompletionClientFactory
{
    public IAiChatCompletionClient GetClient(string provider) => client;
}
