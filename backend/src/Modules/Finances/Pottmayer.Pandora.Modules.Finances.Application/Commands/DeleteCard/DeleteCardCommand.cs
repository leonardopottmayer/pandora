using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteCard;

public sealed record DeleteCardInput(Guid UserId, Guid CardId);

public sealed class DeleteCardCommand(DeleteCardInput input) : CommandBase<DeleteCardInput, bool>(input);
