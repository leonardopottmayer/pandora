using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UnlinkTag;

public sealed class UnlinkTagCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UnlinkTagCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(UnlinkTagCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (!TaggableEntityType.IsSupported(input.EntityType))
            return Fail(TagErrors.InvalidEntityType(input.EntityType));

        var entityType = TaggableEntityType.FromValue(input.EntityType);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var tags = ctx.AcquireRepository<ITagRepository>();
            var links = ctx.AcquireRepository<ITagLinkRepository>();

            var tag = await tags.FindByIdForUserAsync(input.TagId, input.UserId, token);
            if (tag is null)
                return Result<bool>.Failure([TagErrors.NotFound]);

            var link = await links.FindAsync(tag.Id, entityType, input.EntityId, token);
            if (link is null)
                return Result<bool>.Failure([TagErrors.LinkNotFound]);

            await links.RemoveAsync(link, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, "tag", tag.Id, "tag.unlinked", now,
                new { entityType = entityType.Value, entityId = input.EntityId }, ct: token);

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
