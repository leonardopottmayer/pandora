using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.DeleteFilter;

public sealed record DeleteFilterInput(Guid UserId, Guid FilterId);

[RequiresPermission(FilesPermissions.Filters.Delete)]
public sealed class DeleteFilterCommand(DeleteFilterInput input)
    : CommandBase<DeleteFilterInput, bool>(input);
