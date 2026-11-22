using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

/// <summary>
/// Picks the import layout for an uploaded file by routing on the destination's bank + file format +
/// account/card type — a deterministic choice driven by the account or card the user selected. Falls
/// back to content-based detection (<see cref="ILayoutDetector"/>) when the destination has no bank
/// set or no layout is registered for that combination.
/// </summary>
public interface IImportLayoutResolver
{
    Task<Result<ImportLayout>> ResolveAsync(
        byte[] fileBytes,
        string fileName,
        string? bankCode,
        bool isCard,
        IReadOnlyList<ImportLayout> systemLayouts,
        CancellationToken ct = default);
}
