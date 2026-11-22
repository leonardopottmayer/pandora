using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Commands.DecideReview;

namespace Pottmayer.Pandora.Modules.Files.Presentation.Requests;

public sealed record SavePreferencesRequest(bool IsEnabled);

/// <param name="ScanTime">Daily, in the device's local time (<c>"03:00"</c>); null scans only on demand.</param>
/// <param name="CaseSensitive">Null takes the device platform's default.</param>
public sealed record AddRootRequest(
    Guid DeviceId,
    string? Name,
    string? LocalPath,
    TimeOnly? ScanTime,
    bool IncludeHidden,
    bool? CaseSensitive);

public sealed record UpdateRootRequest(string? Name, TimeOnly? ScanTime, bool IncludeHidden, bool CaseSensitive);

public sealed record SaveSelectionRequest(IReadOnlyList<MarkConfig>? Marks);

public sealed record FilterRequest(
    Guid? DeviceId,
    Guid? RootId,
    string? ScopePath,
    string? Name,
    string? Action,
    string? AppliesTo,
    string? Matcher,
    string? Pattern,
    bool? CaseSensitive,
    bool IsEnabled = true);

public sealed record ReviewDecisionRequest(IReadOnlyList<Guid>? EntryIds, IReadOnlyList<ReviewFolder>? Folders);
