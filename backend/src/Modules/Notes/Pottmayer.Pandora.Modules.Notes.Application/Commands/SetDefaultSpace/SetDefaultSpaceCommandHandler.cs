using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetDefaultSpace;

public sealed class SetDefaultSpaceCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SetDefaultSpaceCommand, SpaceDto>
{
    protected override async Task<Result<SpaceDto>> HandleAsync(SetDefaultSpaceCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // Demote the current default first, in its own transaction, so the partial unique index (one
        // default per user) never sees two at once. Guarded by the target existing, so a bad id does not
        // leave the user with no default.
        var found = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = ctx.AcquireRepository<ISpaceRepository>();
            if (await spaces.FindByIdForUserAsync(input.SpaceId, input.UserId, token) is null)
                return false;

            if (await spaces.FindDefaultAsync(input.UserId, token) is { } current && current.Id != input.SpaceId)
            {
                current.SetDefault(false);
                await spaces.UpdateAsync(current, token);
            }
            return true;
        }, cancellationToken: ct);

        if (!found)
            return Fail(SpaceErrors.NotFound);

        var dto = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = ctx.AcquireRepository<ISpaceRepository>();
            var space = (await spaces.FindByIdForUserAsync(input.SpaceId, input.UserId, token))!;

            // The default is where pages go unasked, so it cannot stay hidden.
            space.SetDefault(true);
            space.Unarchive();
            await spaces.UpdateAsync(space, token);

            var stats = await ctx.AcquireRepository<IPageRepository>().GetSpaceStatsAsync(input.UserId, token);
            return SpaceDto.From(space, stats.GetValueOrDefault(space.Id));
        }, cancellationToken: ct);

        return Ok(dto);
    }
}
