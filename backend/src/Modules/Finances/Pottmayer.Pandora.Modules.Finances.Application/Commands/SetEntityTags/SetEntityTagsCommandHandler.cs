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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetEntityTags;

/// <summary>
/// Replaces the whole tag set of one entity (the <c>PUT .../{entity}/{id}/tags</c> shortcut). Adds
/// the missing links and removes the ones no longer wanted, auditing each change. Every supplied tag
/// id must be an existing tag of the user; the target entity must exist and belong to the user.
/// </summary>
public sealed class SetEntityTagsCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetEntityTagsCommand, IReadOnlyList<TagDto>>
{
    protected override async Task<Result<IReadOnlyList<TagDto>>> HandleAsync(
        SetEntityTagsCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (!TaggableEntityType.IsSupported(input.EntityType))
            return Fail(TagErrors.InvalidEntityType(input.EntityType));

        var entityType = TaggableEntityType.FromValue(input.EntityType);
        var desiredIds = input.TagIds.Distinct().ToList();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var tags = ctx.AcquireRepository<ITagRepository>();
            var links = ctx.AcquireRepository<ITagLinkRepository>();

            if (!await TagTargets.ExistsForUserAsync(ctx, entityType, input.EntityId, input.UserId, token))
                return Result<IReadOnlyList<Tag>>.Failure([TagErrors.TargetNotFound]);

            var desiredTags = await tags.GetByIdsForUserAsync(input.UserId, desiredIds, token);
            if (desiredTags.Count != desiredIds.Count)
                return Result<IReadOnlyList<Tag>>.Failure([TagErrors.NotFound]); // an id was unknown or foreign

            var existing = await links.GetByEntityAsync(entityType, input.EntityId, token);
            var existingTagIds = existing.Select(l => l.TagId).ToHashSet();
            var desiredSet = desiredIds.ToHashSet();

            // Diff against the current links: only the additions and removals actually touch the
            // database and get an event — tags already in both sets are left untouched.
            foreach (var link in existing.Where(l => !desiredSet.Contains(l.TagId)))
            {
                await links.RemoveAsync(link, token);
                await ctx.RecordAsync(
                    input.UserId, input.UserId, TagEvents.EntityType, link.TagId, TagEvents.Unlinked, now,
                    new { entityType = entityType.Value, entityId = input.EntityId }, ct: token);
            }

            foreach (var tag in desiredTags.Where(t => !existingTagIds.Contains(t.Id)))
            {
                await links.AddAsync(TagLink.Create(tag.Id, entityType, input.EntityId, timeProvider), token);
                await ctx.RecordAsync(
                    input.UserId, input.UserId, TagEvents.EntityType, tag.Id, TagEvents.Linked, now,
                    new { entityType = entityType.Value, entityId = input.EntityId }, ct: token);
            }

            return Result<IReadOnlyList<Tag>>.Success(desiredTags);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok((IReadOnlyList<TagDto>)[.. result.Value!.Select(TagDto.From)]);
    }
}
