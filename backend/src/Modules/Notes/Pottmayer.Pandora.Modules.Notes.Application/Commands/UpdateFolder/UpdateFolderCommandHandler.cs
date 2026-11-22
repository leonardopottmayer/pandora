using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdateFolder;

public sealed class UpdateFolderCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UpdateFolderCommand, FolderDto>
{
    protected override async Task<Result<FolderDto>> HandleAsync(UpdateFolderCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(FolderErrors.InvalidName);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = ctx.AcquireRepository<IFolderRepository>();
            var folder = await folders.FindByIdForUserAsync(input.FolderId, input.UserId, token);
            if (folder is null)
                return Result<FolderDto>.Failure([FolderErrors.NotFound]);

            folder.Update(input.Name, input.Icon, input.Description);
            await folders.UpdateAsync(folder, token);
            return Result<FolderDto>.Success(FolderDto.From(folder));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
