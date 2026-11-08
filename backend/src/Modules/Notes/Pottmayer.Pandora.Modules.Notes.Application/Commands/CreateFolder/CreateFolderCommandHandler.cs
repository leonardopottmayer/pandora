using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateFolder;

public sealed class CreateFolderCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateFolderCommand, FolderDto>
{
    protected override async Task<Result<FolderDto>> HandleAsync(CreateFolderCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(FolderErrors.InvalidName);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = ctx.AcquireRepository<IFolderRepository>();

            Guid spaceId;
            if (input.ParentId is { } parentId)
            {
                var parent = await folders.FindByIdForUserAsync(parentId, input.UserId, token);
                if (parent is null)
                    return Result<FolderDto>.Failure([FolderErrors.ParentNotFound]);
                spaceId = parent.SpaceId;
            }
            else
            {
                var space = await SpaceResolver.ResolveAsync(
                    ctx.AcquireRepository<ISpaceRepository>(), input.UserId, input.SpaceId, timeProvider, token);
                if (space.IsFailure)
                    return Result<FolderDto>.Failure([.. space.Errors]);
                spaceId = space.Value!.Id;
            }

            var siblings = (await folders.GetForUserAsync(input.UserId, spaceId, token)).Count(f => f.ParentId == input.ParentId);
            var folder = Folder.Create(input.UserId, spaceId, input.ParentId, input.Name, input.Icon, input.Description, siblings, timeProvider);
            await folders.AddAsync(folder, token);
            return Result<FolderDto>.Success(FolderDto.From(folder));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
