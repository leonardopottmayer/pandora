using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.RemoveRoot;

public sealed record RemoveRootInput(Guid UserId, Guid RootId);

[RequiresPermission(FilesPermissions.Roots.Delete)]
public sealed class RemoveRootCommand(RemoveRootInput input)
    : CommandBase<RemoveRootInput, bool>(input);
