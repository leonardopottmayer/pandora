using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

/// <summary>
/// Inspects raw file bytes and returns the matching system layout, or a failure when no layout
/// can be identified. Detection is purely content-based — no user configuration required.
/// </summary>
public interface ILayoutDetector
{
    Task<Result<ImportLayout>> DetectAsync(
        byte[] fileBytes, string fileName, IReadOnlyList<ImportLayout> systemLayouts,
        CancellationToken ct = default);
}
