using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailboxNow;

public sealed class SyncMailboxNowCommandHandler(IUnitOfWorkFactory factory, IServiceProvider services, ISender sender)
    : CommandHandlerBase<SyncMailboxNowCommand, MailboxDto>
{
    protected override async Task<Result<MailboxDto>> HandleAsync(SyncMailboxNowCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var mailbox = await MailboxReads.FindAsync(factory, input.UserId, input.MailboxId, ct);
        if (mailbox is null)
            return Fail(MailErrors.MailboxNotFound);

        if (mailbox.Status == MailboxStatus.NeedsReconnect)
        {
            var test = await services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value).TestConnectionAsync(mailbox, ct);
            if (test.IsFailure)
                return Result<MailboxDto>.Failure(test.Errors);

            await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            {
                var repository = ctx.AcquireRepository<IMailboxRepository>();
                if (await repository.FindForUserAsync(mailbox.Id, input.UserId, token) is not { } tracked)
                    return;
                tracked.Reconnect();
                await repository.UpdateAsync(tracked, token);
            }, cancellationToken: ct);
        }

        // A failed cycle is recorded on the mailbox (status, last error), which is what the screen shows.
        await sender.Send(new SyncMailboxCommand(new SyncMailboxInput(mailbox.Id)), ct);

        return await MailboxReads.LoadDtoAsync(factory, input.UserId, input.MailboxId, ct) is { } dto
            ? Ok(dto)
            : Fail(MailErrors.MailboxNotFound);
    }
}
