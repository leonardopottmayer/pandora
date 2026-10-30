using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ConnectMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.UpdateMailbox;

public sealed class UpdateMailboxCommandHandler(IUnitOfWorkFactory factory, IServiceProvider services)
    : CommandHandlerBase<UpdateMailboxCommand, MailboxDto>
{
    protected override async Task<Result<MailboxDto>> HandleAsync(UpdateMailboxCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.DisplayName?.Trim().Length > Mailbox.DisplayNameMaxLength)
            return Fail(MailErrors.DisplayNameTooLong);

        var server = MailboxServer.From(input.ImapHost, input.ImapPort, input.SmtpHost, input.SmtpPort, out var serverValid);
        if (!serverValid)
            return Fail(MailErrors.ServerSettingsInvalid);

        var mailbox = await MailboxReads.FindAsync(factory, input.UserId, input.MailboxId, ct);
        if (mailbox is null)
            return Fail(MailErrors.MailboxNotFound);

        var changesServer = server is not null && mailbox.Provider.UsesServerSettings && server != mailbox.Server;
        if (changesServer)
        {
            mailbox.Update(input.DisplayName, server);
            var test = await services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value).TestConnectionAsync(mailbox, ct);
            if (test.IsFailure)
                return Result<MailboxDto>.Failure(test.Errors);
        }

        await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var repository = ctx.AcquireRepository<IMailboxRepository>();
            var tracked = await repository.FindForUserAsync(input.MailboxId, input.UserId, token);
            if (tracked is null)
                return;

            tracked.Update(input.DisplayName, server);
            if (changesServer)
                tracked.Reconnect();
            await repository.UpdateAsync(tracked, token);
        }, cancellationToken: ct);

        return await MailboxReads.LoadDtoAsync(factory, input.UserId, input.MailboxId, ct) is { } dto
            ? Ok(dto)
            : Fail(MailErrors.MailboxNotFound);
    }
}
