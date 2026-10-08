using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.RemoveRoot;

public sealed record RemoveRootInput(Guid UserId, Guid RootId);

public sealed class RemoveRootCommand(RemoveRootInput input)
    : CommandBase<RemoveRootInput, bool>(input);
