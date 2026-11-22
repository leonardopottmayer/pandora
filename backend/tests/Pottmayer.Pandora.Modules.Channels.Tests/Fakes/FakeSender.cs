using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// An <see cref="ISender"/> that refuses to run. The triage paths under test never dispatch a
/// command; a call here means a test exercised a path it did not intend to.
/// </summary>
internal sealed class FakeSender : ISender
{
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Unexpected Send of {request.GetType().Name}.");
}
