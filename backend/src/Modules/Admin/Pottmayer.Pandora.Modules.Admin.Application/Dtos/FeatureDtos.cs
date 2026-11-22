using Pottmayer.Tars.FeatureFlags.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

internal static class FeatureDtos
{
    public static IReadOnlyList<FeatureDto> From(IEnumerable<FeatureFlagState> states) =>
    [
        .. states.Select(s => new FeatureDto(
            s.Definition.Key, s.Definition.Parent, s.Definition.DefaultEnabled, s.Override, s.Enabled))
    ];
}
