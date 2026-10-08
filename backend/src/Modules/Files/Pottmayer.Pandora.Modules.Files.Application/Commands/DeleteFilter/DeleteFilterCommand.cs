using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.DeleteFilter;

public sealed record DeleteFilterInput(Guid UserId, Guid FilterId);

public sealed class DeleteFilterCommand(DeleteFilterInput input)
    : CommandBase<DeleteFilterInput, bool>(input);
