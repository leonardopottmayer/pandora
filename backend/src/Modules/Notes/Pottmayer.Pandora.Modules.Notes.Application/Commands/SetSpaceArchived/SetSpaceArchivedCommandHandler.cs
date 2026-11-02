using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetSpaceArchived;

public sealed class SetSpaceArchivedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetSpaceArchivedCommand, SpaceDto>
{
    protected override async Task<Result<SpaceDto>> HandleAsync(SetSpaceArchivedCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = ctx.AcquireRepository<ISpaceRepository>();
            var space = await spaces.FindByIdForUserAsync(input.SpaceId, input.UserId, token);
            if (space is null)
                return Result<SpaceDto>.Failure([SpaceErrors.NotFound]);

            if (input.Archived)
            {
                if (space.IsDefault)
                    return Result<SpaceDto>.Failure([SpaceErrors.IsDefault]);
                space.Archive(timeProvider);
            }
            else
            {
                space.Unarchive();
            }
            await spaces.UpdateAsync(space, token);

            var stats = await ctx.AcquireRepository<IPageRepository>().GetSpaceStatsAsync(input.UserId, token);
            return Result<SpaceDto>.Success(SpaceDto.From(space, stats.GetValueOrDefault(space.Id)));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
