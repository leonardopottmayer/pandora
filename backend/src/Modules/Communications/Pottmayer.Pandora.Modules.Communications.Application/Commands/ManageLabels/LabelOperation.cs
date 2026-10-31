using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetLabels;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;

/// <summary>
/// What the label commands share. The provider first, under the action gate, then the labels + changes step: there is
/// one path for what a label operation does locally, the same one a change made in the webmail takes.
/// </summary>
public sealed class LabelOperation(
    IUnitOfWorkFactory factory,
    IServiceProvider services,
    MailboxGates gates,
    MailboxChangesStep changes,
    ISender sender,
    ILogger<LabelOperation> logger)
{
    /// <summary>The name to send to the provider, or null when it cannot be used.</summary>
    public static string? ValidName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MailLabel.NameMaxLength ? null : trimmed;
    }

    public Task<Mailbox?> FindMailboxAsync(Guid userId, Guid mailboxId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(mailboxId, userId, token), cancellationToken: ct);

    /// <summary>A user's own label, with its mailbox. A system label is refused here, before the provider hears of it.</summary>
    public async Task<Result<(Mailbox Mailbox, MailLabel Label)>> FindUserLabelAsync(Guid userId, Guid labelId, CancellationToken ct)
    {
        var found = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var label = await ctx.AcquireRepository<IMailLabelRepository>().GetByIdAsync(labelId, token);
            if (label is null)
                return null;

            var mailbox = await ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(label.MailboxId, userId, token);
            return mailbox is null ? null : new Found(mailbox, label);
        }, cancellationToken: ct);

        if (found is null)
            return Result<(Mailbox, MailLabel)>.Failure(MailErrors.LabelNotFound);
        if (found.Label.Kind != LabelKind.User)
            return Result<(Mailbox, MailLabel)>.Failure(MailErrors.SystemLabel);

        return Result<(Mailbox, MailLabel)>.Success((found.Mailbox, found.Label));
    }

    public async Task<Result<IReadOnlyList<LabelDto>>> RunAsync<T>(
        Guid userId, Mailbox mailbox, Func<IMailboxProvider, Task<Result<T>>> call, CancellationToken ct)
    {
        var provider = services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value);

        var gate = gates.Action(mailbox.Id);
        await gate.WaitAsync(ct);
        try
        {
            var result = await call(provider);
            if (result.IsFailure)
                return Result<IReadOnlyList<LabelDto>>.Failure(result.Errors);
        }
        finally
        {
            gate.Release();
        }

        // The provider has accepted, so the operation succeeded: if this fails, the next cycle catches up.
        var updated = await changes.RunAsync(mailbox.Id, provider, ct);
        if (updated.IsFailure)
            logger.LogWarning("Mailbox {MailboxId} labels not refreshed after an operation: {Error}.", mailbox.Id, updated.Errors[0].Message);

        return await sender.Send(new GetLabelsQuery(new GetLabelsInput(userId, mailbox.Id)), ct);
    }

    private sealed record Found(Mailbox Mailbox, MailLabel Label);
}
