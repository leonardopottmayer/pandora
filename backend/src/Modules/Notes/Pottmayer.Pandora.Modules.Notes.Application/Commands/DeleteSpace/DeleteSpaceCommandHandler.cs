using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteSpace;

public sealed class DeleteSpaceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteSpaceCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteSpaceCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = ctx.AcquireRepository<ISpaceRepository>();
            var space = await spaces.FindByIdForUserAsync(input.SpaceId, input.UserId, token);
            if (space is null)
                return Result<bool>.Failure([SpaceErrors.NotFound]);
            if (space.IsDefault)
                return Result<bool>.Failure([SpaceErrors.IsDefault]);

            // Archived pages count: they still live in the space and would point at a deleted row.
            var stats = await ctx.AcquireRepository<IPageRepository>().GetSpaceStatsAsync(input.UserId, token);
            if (stats.ContainsKey(space.Id))
                return Result<bool>.Failure([SpaceErrors.NotEmpty]);

            space.Delete(timeProvider);
            await spaces.UpdateAsync(space, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
