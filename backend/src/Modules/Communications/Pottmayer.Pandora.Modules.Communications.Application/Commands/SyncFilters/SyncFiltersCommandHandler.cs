using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncFilters;

/// <summary>
/// The diff of §10.3, by filter id. A disabled rule has no filter id, so it is not in it. A filter id does not change
/// while the filter lives (Gmail has no update), so a filter already known has nothing to bring.
/// </summary>
public sealed class SyncFiltersCommandHandler(IUnitOfWorkFactory factory, IServiceProvider services)
    : CommandHandlerBase<SyncFiltersCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SyncFiltersCommand request, CancellationToken ct)
    {
        var mailboxId = request.Input.MailboxId;
        var mailbox = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IMailboxRepository>().GetByIdAsync(mailboxId, token), cancellationToken: ct);
        if (mailbox is null)
            return Ok(false);

        var filters = await services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value).ListFiltersAsync(mailbox, ct);
        if (filters.IsFailure)
            return Result<bool>.Failure(filters.Errors);

        await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ApplyAsync(ctx, mailboxId, filters.Value, token), cancellationToken: ct);
        return Ok(true);
    }

    private static async Task ApplyAsync(IDataContext context, Guid mailboxId, IReadOnlyList<RemoteFilter> filters, CancellationToken ct)
    {
        var repository = context.AcquireRepository<IMailRuleRepository>();
        var rules = await repository.ListByMailboxAsync(mailboxId, ct);
        var bound = rules.Where(r => r.ProviderFilterId is not null).ToList();

        var gone = bound.Where(r => filters.All(f => f.ProviderFilterId != r.ProviderFilterId)).ToList();
        var fresh = filters.Where(f => bound.All(r => r.ProviderFilterId != f.ProviderFilterId)).ToList();
        if (gone.Count == 0 && fresh.Count == 0)
            return;

        var labels = (await context.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailboxId, ct))
            .ToDictionary(l => l.ProviderLabelId, l => l.Id);

        // ponytail: one gone + one new in a cycle is read as an edit, since Gmail recreates a filter to edit it. Two edits in
        // one cycle lose their names, and an unrelated delete + create is taken for an edit. Gmail gives nothing better to go on.
        if (gone.Count == 1 && fresh.Count == 1)
        {
            var filter = fresh[0];
            if (ToRuleActions(filter, labels) is { } actions)
                gone[0].FollowFilter(filter.ProviderFilterId, filter.Conditions, actions);
            else
                gone[0].FollowReadOnlyFilter(filter.ProviderFilterId, filter.Raw);
            await repository.UpdateAsync(gone[0], ct);
            return;
        }

        await repository.DeleteByIdsAsync(gone.Select(r => r.Id).ToList(), ct);

        var position = rules.Count == 0 ? 0 : rules.Max(r => r.Position) + 1;
        foreach (var filter in fresh)
            await repository.AddAsync(Import(mailboxId, filter, position++, labels), ct);
    }

    private static MailRule Import(Guid mailboxId, RemoteFilter filter, int position, Dictionary<string, Guid> labels)
    {
        if (ToRuleActions(filter, labels) is not { } actions)
            return MailRule.ImportReadOnly(mailboxId, filter.Name, position, filter.ProviderFilterId, filter.Raw);

        var rule = MailRule.Create(mailboxId, filter.Name, position, filter.Conditions, actions, RuleExecution.ProviderFilter);
        rule.BindFilter(filter.ProviderFilterId);
        return rule;
    }

    /// <summary>Null when a rule cannot hold the filter, or it points at a label not known here: read-only, never dropped.</summary>
    private static List<MailRuleAction>? ToRuleActions(RemoteFilter filter, Dictionary<string, Guid> labels)
    {
        if (filter.IsReadOnly)
            return null;

        var actions = new List<MailRuleAction>();
        foreach (var action in filter.Actions)
        {
            if (action.ProviderLabelId is null)
                actions.Add(new MailRuleAction(action.Kind, null));
            else if (labels.TryGetValue(action.ProviderLabelId, out var labelId))
                actions.Add(new MailRuleAction(action.Kind, labelId));
            else
                return null;
        }

        return actions;
    }
}
