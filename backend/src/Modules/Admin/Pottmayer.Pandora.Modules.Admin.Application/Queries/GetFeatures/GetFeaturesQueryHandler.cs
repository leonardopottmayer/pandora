using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.FeatureFlags.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetFeatures;

public sealed class GetFeaturesQueryHandler(IFeatureFlags flags)
    : QueryHandlerBase<GetFeaturesQuery, IReadOnlyList<FeatureDto>>
{
    protected override async Task<Result<IReadOnlyList<FeatureDto>>> HandleAsync(GetFeaturesQuery request, CancellationToken ct)
    {
        var states = await flags.GetAllAsync(ct);
        return Ok(FeatureDtos.From(states));
    }
}
