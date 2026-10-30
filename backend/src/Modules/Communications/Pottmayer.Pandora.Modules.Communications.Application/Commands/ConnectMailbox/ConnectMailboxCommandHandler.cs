using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ConnectMailbox;

public sealed class ConnectMailboxCommandHandler(
    IUnitOfWorkFactory factory,
    IExternalAccountReader accounts,
    IServiceProvider services) : CommandHandlerBase<ConnectMailboxCommand, MailboxDto>
{
    protected override async Task<Result<MailboxDto>> HandleAsync(ConnectMailboxCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!MailProvider.IsSupported(input.Provider))
            return Fail(MailErrors.UnsupportedProvider(input.Provider));
        var provider = MailProvider.FromValue(input.Provider!);

        var account = await accounts.GetAsync(input.UserId, input.ExternalAccountId, ct);
        if (account is null)
            return Fail(MailErrors.AccountNotFound);
        if (account.Provider != provider.AccountProvider)
            return Fail(MailErrors.AccountProviderMismatch(provider.Value));

        var address = (input.Address ?? (provider == MailProvider.Gmail ? account.DisplayName : account.ProviderAccountId))?.Trim();
        if (!IsAddress(address))
            return Fail(MailErrors.AddressRequired);

        var server = MailboxServer.From(input.ImapHost, input.ImapPort, input.SmtpHost, input.SmtpPort, out var serverValid);
        if (!serverValid || (provider.UsesServerSettings && server is null && provider.Preset is null))
            return Fail(MailErrors.ServerSettingsInvalid);

        var mailbox = Mailbox.Connect(input.UserId, provider, account.Id, address!, input.DisplayName, server);

        var taken = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            (await ctx.AcquireRepository<IMailboxRepository>().ListByExternalAccountAsync(account.Id, token))
            .Any(m => m.UserId == input.UserId), cancellationToken: ct);
        if (taken)
            return Fail(MailErrors.AlreadyConnected);

        var test = await services.GetRequiredKeyedService<IMailboxProvider>(provider.Value).TestConnectionAsync(mailbox, ct);
        if (test.IsFailure)
            return Result<MailboxDto>.Failure(test.Errors);

        await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IMailboxRepository>().AddAsync(mailbox, token), cancellationToken: ct);

        return Ok(MailboxDto.From(mailbox, 0));
    }

    private static bool IsAddress(string? address) =>
        address is { Length: > 2 and <= Mailbox.AddressMaxLength } && address.Contains('@') && !address.Any(char.IsWhiteSpace);
}
