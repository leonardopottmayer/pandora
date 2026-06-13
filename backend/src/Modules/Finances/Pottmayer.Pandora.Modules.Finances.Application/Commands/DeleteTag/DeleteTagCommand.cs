using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteTag;

public sealed record DeleteTagInput(Guid UserId, Guid TagId);

public sealed class DeleteTagCommand(DeleteTagInput input)
    : CommandBase<DeleteTagInput, bool>(input);
