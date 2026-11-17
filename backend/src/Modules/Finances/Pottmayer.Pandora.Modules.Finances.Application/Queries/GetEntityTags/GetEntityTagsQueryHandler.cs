using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetEntityTags;

public sealed class GetEntityTagsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetEntityTagsQuery, IReadOnlyList<Guid>>
{
    protected override async Task<Result<IReadOnlyList<Guid>>> HandleAsync(GetEntityTagsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        if (!TaggableEntityType.IsSupported(input.EntityType))
            return Fail(TagErrors.InvalidEntityType(input.EntityType));

        var entityType = TaggableEntityType.FromValue(input.EntityType);
        return await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            if (!await TagTargets.ExistsForUserAsync(ctx, entityType, input.EntityId, input.UserId, token))
                return Result<IReadOnlyList<Guid>>.Failure([TagErrors.TargetNotFound]);

            var links = await ctx.AcquireRepository<ITagLinkRepository>().GetByEntityAsync(entityType, input.EntityId, token);
            return Result<IReadOnlyList<Guid>>.Success([.. links.Select(l => l.TagId)]);
        }, cancellationToken: ct);
    }
}
