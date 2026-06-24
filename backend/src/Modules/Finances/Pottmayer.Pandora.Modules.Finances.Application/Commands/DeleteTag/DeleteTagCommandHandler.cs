using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteTag;

public sealed class DeleteTagCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteTagCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteTagCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var tags = ctx.AcquireRepository<ITagRepository>();
            var links = ctx.AcquireRepository<ITagLinkRepository>();

            var tag = await tags.FindByIdForUserAsync(input.TagId, input.UserId, token);
            if (tag is null)
                return Result<bool>.Failure([TagErrors.NotFound]);

            // Read the links first so the audit records what was severed, then drop the tag — the
            // DB FK (ON DELETE CASCADE) removes the links (criterion 4). We let the cascade do it
            // rather than deleting them here too: EF has no modelled relationship between the two,
            // so a double delete races the cascade and trips optimistic concurrency.
            var existing = await links.GetByTagAsync(tag.Id, token);

            await tags.RemoveAsync(tag, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TagEvents.EntityType, tag.Id, TagEvents.Deleted, now,
                new
                {
                    name = tag.Name,
                    removedLinks = existing
                        .Select(l => new { entityType = l.EntityType.Value, entityId = l.EntityId })
                        .ToList()
                },
                ct: token);

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
