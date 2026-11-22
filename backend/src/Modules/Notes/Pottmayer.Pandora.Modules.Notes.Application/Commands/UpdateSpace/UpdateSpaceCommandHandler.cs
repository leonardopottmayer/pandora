using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdateSpace;

public sealed class UpdateSpaceCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UpdateSpaceCommand, SpaceDto>
{
    protected override async Task<Result<SpaceDto>> HandleAsync(UpdateSpaceCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (SpaceValidation.Check(input.Name, input.Color) is { } error)
            return Fail(error);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = ctx.AcquireRepository<ISpaceRepository>();
            var space = await spaces.FindByIdForUserAsync(input.SpaceId, input.UserId, token);
            if (space is null)
                return Result<SpaceDto>.Failure([SpaceErrors.NotFound]);

            space.Update(input.Name, input.Icon, input.Color, input.Description);
            await spaces.UpdateAsync(space, token);

            var stats = await ctx.AcquireRepository<IPageRepository>().GetSpaceStatsAsync(input.UserId, token);
            return Result<SpaceDto>.Success(SpaceDto.From(space, stats.GetValueOrDefault(space.Id)));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
