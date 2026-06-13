using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkTag;

public sealed class LinkTagCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<LinkTagCommand, TagLinkDto>
{
    protected override async Task<Result<TagLinkDto>> HandleAsync(LinkTagCommand request, CancellationToken ct)
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
                return Result<TagLink>.Failure([TagErrors.NotFound]);

            if (!await TagTargets.ExistsForUserAsync(ctx, entityType, input.EntityId, input.UserId, token))
                return Result<TagLink>.Failure([TagErrors.TargetNotFound]);

            // Idempotent: the trio is unique, so a repeat link returns the existing one (no event).
            var existing = await links.FindAsync(tag.Id, entityType, input.EntityId, token);
            if (existing is not null)
                return Result<TagLink>.Success(existing);

            var link = TagLink.Create(tag.Id, entityType, input.EntityId, timeProvider);
            await links.AddAsync(link, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, "tag", tag.Id, "tag.linked", now,
                new { entityType = entityType.Value, entityId = input.EntityId }, ct: token);

            return Result<TagLink>.Success(link);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TagLinkDto.From(result.Value!));
    }
}
