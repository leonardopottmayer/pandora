using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateSpace;

public sealed class CreateSpaceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateSpaceCommand, SpaceDto>
{
    protected override async Task<Result<SpaceDto>> HandleAsync(CreateSpaceCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (SpaceValidation.Check(input.Name, input.Color) is { } error)
            return Fail(error);

        var space = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = ctx.AcquireRepository<ISpaceRepository>();
            var isFirst = await spaces.FindDefaultAsync(input.UserId, token) is null;

            var created = Space.Create(
                input.UserId, input.Name, input.Icon, input.Color, input.Description, isDefault: isFirst, timeProvider);
            await spaces.AddAsync(created, token);
            return created;
        }, cancellationToken: ct);

        return Ok(SpaceDto.From(space, stats: null));
    }
}
