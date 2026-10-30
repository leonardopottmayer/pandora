using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.DeleteMailbox;

public sealed class DeleteMailboxCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<DeleteMailboxCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteMailboxCommand request, CancellationToken ct)
    {
        var input = request.Input;
        return await factory.ExecuteAsync<Result<bool>>(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var repository = ctx.AcquireRepository<IMailboxRepository>();
            var mailbox = await repository.FindForUserAsync(input.MailboxId, input.UserId, token);
            if (mailbox is null)
                return MailErrors.MailboxNotFound;

            // The database cascades labels, messages and cached bodies.
            await repository.RemoveAsync(mailbox, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);
    }
}
