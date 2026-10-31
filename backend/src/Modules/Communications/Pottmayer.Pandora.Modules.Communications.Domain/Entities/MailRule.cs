using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Entities;

/// <summary>One condition; all of a rule's must match. <see cref="RuleConditionKinds"/> has the kinds.</summary>
public sealed record MailRuleCondition(string Kind, string Value);

/// <summary>One action. Only adding a label and moving carry a label.</summary>
public sealed record MailRuleAction(string Kind, Guid? LabelId);

public static class RuleConditionKinds
{
    public const string From = "from";
    public const string To = "to";
    public const string Subject = "subject";
    public const string HasWords = "has-words";
    public const string HasAttachment = "has-attachment";

    /// <summary>Gmail's raw search string, so an imported filter loses nothing. Gmail mailboxes only.</summary>
    public const string ProviderQuery = "provider-query";

    public static readonly IReadOnlyList<string> All = [From, To, Subject, HasWords, HasAttachment, ProviderQuery];
}

public static class RuleActionKinds
{
    public const string AddLabel = "add-label";
    public const string Move = "move";
    public const string Archive = "archive";
    public const string MarkRead = "mark-read";
    public const string Star = "star";
    public const string Trash = "trash";

    public static readonly IReadOnlyList<string> All = [AddLabel, Move, Archive, MarkRead, Star, Trash];
}

/// <summary>A rule of one mailbox: what to look for and what to do. See <see cref="RuleExecution"/> for where it runs.</summary>
public sealed class MailRule
{
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; }
    public Guid MailboxId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Position { get; private set; }
    public List<MailRuleCondition> Conditions { get; private set; } = [];
    public List<MailRuleAction> Actions { get; private set; } = [];
    public RuleExecution Execution { get; private set; } = RuleExecution.Pandora;

    /// <summary>Null while disabled: Gmail has no disabled filters.</summary>
    public string? ProviderFilterId { get; private set; }

    /// <summary>The filter as the provider sent it, for one that does not fit the model. Never rewritten.</summary>
    public string? ProviderFilterRaw { get; private set; }

    public bool IsEnabled { get; private set; }
    public RuleStatus Status { get; private set; } = RuleStatus.Ok;
    public string? LastError { get; private set; }

    private MailRule() { }

    public bool IsReadOnly => Status == RuleStatus.ReadOnly;

    /// <summary>Whether it runs in Pandora's engine right now.</summary>
    public bool RunsInPandora => IsEnabled && Execution == RuleExecution.Pandora && Status == RuleStatus.Ok;

    /// <summary>
    /// What makes a rule acceptable, before any provider hears of it. An empty rule matches all mail, and could trash it.
    /// </summary>
    public static Error? Validate(
        string? name, IReadOnlyCollection<MailRuleCondition> conditions, IReadOnlyCollection<MailRuleAction> actions,
        MailProvider provider, IReadOnlyDictionary<Guid, MailLabel> labels)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
            return MailErrors.RuleNameInvalid;

        if (conditions.Count == 0 || conditions.Any(c => !RuleConditionKinds.All.Contains(c.Kind)))
            return MailErrors.RuleConditionsInvalid;
        if (conditions.Any(c => c.Kind != RuleConditionKinds.HasAttachment && string.IsNullOrWhiteSpace(c.Value)))
            return MailErrors.RuleConditionsInvalid;
        if (provider != MailProvider.Gmail && conditions.Any(c => c.Kind == RuleConditionKinds.ProviderQuery))
            return MailErrors.RuleConditionsInvalid;

        if (actions.Count == 0 || actions.Any(a => !RuleActionKinds.All.Contains(a.Kind)))
            return MailErrors.RuleActionsInvalid;

        var labelled = actions.Where(a => a.Kind is RuleActionKinds.AddLabel or RuleActionKinds.Move).ToList();
        if (labelled.Any(a => a.LabelId is not { } id || !labels.ContainsKey(id)))
            return MailErrors.RuleActionsInvalid;

        // A folder is where a message lives, so "add" is not something IMAP can do.
        if (provider != MailProvider.Gmail && actions.Any(a => a.Kind == RuleActionKinds.AddLabel))
            return MailErrors.RuleActionsInvalid;

        // "Only one user-defined label is allowed per filter."
        if (provider == MailProvider.Gmail && labelled.Select(a => a.LabelId).Distinct().Count() > 1)
            return MailErrors.RuleActionsInvalid;

        return null;
    }

    public static MailRule Create(
        Guid mailboxId, string name, int position, IEnumerable<MailRuleCondition> conditions, IEnumerable<MailRuleAction> actions,
        RuleExecution execution) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            MailboxId = mailboxId,
            Name = name.Trim(),
            Position = position,
            Conditions = [.. conditions],
            Actions = [.. actions],
            Execution = execution,
            IsEnabled = true,
        };

    /// <summary>A provider filter the model cannot express: shown, never rewritten.</summary>
    public static MailRule ImportReadOnly(Guid mailboxId, string name, int position, string providerFilterId, string raw) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            MailboxId = mailboxId,
            Name = name.Trim(),
            Position = position,
            Execution = RuleExecution.ProviderFilter,
            ProviderFilterId = providerFilterId,
            ProviderFilterRaw = raw,
            IsEnabled = true,
            Status = RuleStatus.ReadOnly,
        };

    public void Update(string name, IEnumerable<MailRuleCondition> conditions, IEnumerable<MailRuleAction> actions)
    {
        Name = name.Trim();
        Conditions = [.. conditions];
        Actions = [.. actions];
        Status = RuleStatus.Ok;
        LastError = null;
    }

    public void MoveTo(int position) => Position = position;

    public void BindFilter(string? providerFilterId) => ProviderFilterId = providerFilterId;

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    /// <summary>The label it points at is gone. A read-only rule stays as it is: the provider decides about those.</summary>
    public void MarkBroken(string error)
    {
        if (IsReadOnly)
            return;

        Status = RuleStatus.Broken;
        LastError = error;
    }

    public void RecordError(string error) => LastError = error;

    public void ClearError() => LastError = null;
}
