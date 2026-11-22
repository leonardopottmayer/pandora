using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MoveFolder;

public sealed class MoveFolderCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<MoveFolderCommand, FolderDto>
{
    protected override async Task<Result<FolderDto>> HandleAsync(MoveFolderCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = ctx.AcquireRepository<IFolderRepository>();
            var folder = await folders.FindByIdForUserAsync(input.FolderId, input.UserId, token);
            if (folder is null)
                return Result<FolderDto>.Failure([FolderErrors.NotFound]);

            if (input.ParentId is { } parentId)
            {
                var all = await folders.GetForUserAsync(input.UserId, folder.SpaceId, token);
                if (all.FirstOrDefault(f => f.Id == parentId) is null)
                    return Result<FolderDto>.Failure([FolderErrors.ParentNotFound]);
                if (Folder.Subtree(folder.Id, all).Any(f => f.Id == parentId))
                    return Result<FolderDto>.Failure([FolderErrors.CycleDetected]);
            }

            folder.Move(input.ParentId, input.OrderIndex);
            await folders.UpdateAsync(folder, token);
            return Result<FolderDto>.Success(FolderDto.From(folder));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
