namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>
/// The outcome of a reachability probe: one minimal round-trip to the provider using the user's key.
/// The operation always succeeds; this payload says whether the <em>probe</em> did. <see cref="ErrorKind"/>
/// separates the reasons a caller acts on differently:
/// <c>no_key</c> (nothing stored in Integrations), <c>rejected</c> (a permanent failure — bad key or
/// model), <c>unreachable</c> (a transient failure — endpoint down, timeout).
/// </summary>
public sealed record ReachabilityResultDto(
    bool Ok,
    long LatencyMs,
    string? Reply,
    string? Error,
    string? ErrorKind);
