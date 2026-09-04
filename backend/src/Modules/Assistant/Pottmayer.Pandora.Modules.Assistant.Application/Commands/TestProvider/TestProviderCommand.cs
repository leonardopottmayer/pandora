using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.TestProvider;

/// <summary>
/// Probes a provider with one minimal round-trip using the user's stored key. <see cref="Model"/> is
/// optional: when null the probe uses the default model.
/// </summary>
public sealed record TestProviderInput(Guid UserId, string Provider, string? Model);

public sealed class TestProviderCommand(TestProviderInput input)
    : CommandBase<TestProviderInput, ReachabilityResultDto>(input);
