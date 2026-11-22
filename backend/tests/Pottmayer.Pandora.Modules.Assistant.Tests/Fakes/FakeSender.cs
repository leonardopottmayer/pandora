using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>Records dispatched requests and returns a preset response.</summary>
internal sealed class FakeSender : ISender
{
    public List<object> Sent { get; } = [];
    public object? Response { get; set; }

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        return ValueTask.FromResult((TResponse)Response!);
    }
}
