using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageRules;

public sealed class CreateRuleCommandHandler(RuleOperation operation, IUnitOfWorkFactory factory)
    : CommandHandlerBase<CreateRuleCommand, IReadOnlyList<RuleDto>>
{
    protected override Task<Result<IReadOnlyList<RuleDto>>> HandleAsync(CreateRuleCommand request, CancellationToken ct)
    {
        var input = request.Input;
        return operation.OnMailboxAsync(input.UserId, input.MailboxId, async (mailbox, provider) =>
        {
            var labels = await operation.LabelsAsync(mailbox.Id, ct);
            if (MailRule.Validate(input.Name, input.Conditions, input.Actions, mailbox.Provider, labels) is { } invalid)
                return invalid;

            string? filterId = null;
            if (provider.Capabilities.ServerFilters)
            {
                var created = await provider.CreateFilterAsync(mailbox, input.Conditions, RuleOperation.ToRemote(input.Actions, labels), ct);
                if (created.IsFailure)
                    return created.Errors[0];
                filterId = created.Value.ProviderFilterId;
            }

            await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            {
                var rules = ctx.AcquireRepository<IMailRuleRepository>();
                var existing = await rules.ListByMailboxAsync(mailbox.Id, token);
                var rule = MailRule.Create(
                    mailbox.Id, input.Name!, existing.Count == 0 ? 0 : existing.Max(r => r.Position) + 1, input.Conditions, input.Actions,
                    provider.Capabilities.ServerFilters ? RuleExecution.ProviderFilter : RuleExecution.Pandora);
                rule.BindFilter(filterId);
                await rules.AddAsync(rule, token);
            }, cancellationToken: ct);
            return null;
        }, ct);
    }
}

public sealed class UpdateRuleCommandHandler(RuleOperation operation)
    : CommandHandlerBase<UpdateRuleCommand, IReadOnlyList<RuleDto>>
{
    private const string OldFilterRemains =
        "The rule is saved, but its previous filter could not be deleted from Gmail. It is still there, and comes back as a rule of its own.";

    protected override Task<Result<IReadOnlyList<RuleDto>>> HandleAsync(UpdateRuleCommand request, CancellationToken ct)
    {
        var input = request.Input;
        return operation.OnRuleAsync(input.UserId, input.RuleId, async (mailbox, rule, provider) =>
        {
            if (rule.IsReadOnly)
                return MailErrors.RuleReadOnly;

            var labels = await operation.LabelsAsync(mailbox.Id, ct);
            if (MailRule.Validate(input.Name, input.Conditions, input.Actions, mailbox.Provider, labels) is { } invalid)
                return invalid;

            var filterId = rule.ProviderFilterId;
            string? replaced = null;
            if (rule.Execution == RuleExecution.ProviderFilter)
            {
                if (!input.IsEnabled && filterId is not null)
                {
                    var deleted = await provider.DeleteFilterAsync(mailbox, filterId, ct);
                    if (deleted.IsFailure)
                        return deleted.Errors[0];
                    filterId = null;
                }
                else if (input.IsEnabled && (filterId is null || !rule.Conditions.SequenceEqual(input.Conditions) || !rule.Actions.SequenceEqual(input.Actions)))
                {
                    var created = await provider.CreateFilterAsync(mailbox, input.Conditions, RuleOperation.ToRemote(input.Actions, labels), ct);
                    if (created.IsFailure)
                        return created.Errors[0];
                    replaced = filterId;
                    filterId = created.Value.ProviderFilterId;
                }
            }

            await operation.UpdateRuleAsync(rule.Id, r =>
            {
                r.Update(input.Name!, input.Conditions, input.Actions);
                r.SetEnabled(input.IsEnabled);
                r.BindFilter(filterId);
            }, ct);

            if (replaced is not null && (await provider.DeleteFilterAsync(mailbox, replaced, ct)).IsFailure)
                await operation.UpdateRuleAsync(rule.Id, r => r.RecordError(OldFilterRemains), ct);

            return null;
        }, ct);
    }
}

public sealed class DeleteRuleCommandHandler(RuleOperation operation, IUnitOfWorkFactory factory)
    : CommandHandlerBase<DeleteRuleCommand, IReadOnlyList<RuleDto>>
{
    protected override Task<Result<IReadOnlyList<RuleDto>>> HandleAsync(DeleteRuleCommand request, CancellationToken ct)
    {
        var input = request.Input;
        return operation.OnRuleAsync(input.UserId, input.RuleId, async (mailbox, rule, provider) =>
        {
            if (rule.ProviderFilterId is { } filterId)
            {
                var deleted = await provider.DeleteFilterAsync(mailbox, filterId, ct);
                if (deleted.IsFailure)
                    return deleted.Errors[0];
            }

            await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
                ctx.AcquireRepository<IMailRuleRepository>().DeleteByIdsAsync([rule.Id], token), cancellationToken: ct);
            return null;
        }, ct);
    }
}

public sealed class ReorderRulesCommandHandler(RuleOperation operation, IUnitOfWorkFactory factory)
    : CommandHandlerBase<ReorderRulesCommand, IReadOnlyList<RuleDto>>
{
    protected override Task<Result<IReadOnlyList<RuleDto>>> HandleAsync(ReorderRulesCommand request, CancellationToken ct)
    {
        var input = request.Input;
        return operation.OnMailboxAsync(input.UserId, input.MailboxId, async (mailbox, _) =>
        {
            await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            {
                var repository = ctx.AcquireRepository<IMailRuleRepository>();
                var rules = await repository.ListByMailboxAsync(mailbox.Id, token);

                var listed = input.RuleIds.ToList();
                var position = 0;
                foreach (var rule in rules.OrderBy(r => listed.IndexOf(r.Id) is >= 0 and var index ? index : int.MaxValue))
                {
                    rule.MoveTo(position++);
                    await repository.UpdateAsync(rule, token);
                }
            }, cancellationToken: ct);
            return null;
        }, ct);
    }
}
