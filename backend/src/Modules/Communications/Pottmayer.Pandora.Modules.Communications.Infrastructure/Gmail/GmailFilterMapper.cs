using System.Text.Json;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Gmail;

/// <summary>
/// A rule as a Gmail filter and back. Gmail expresses every action as labels: moving is adding one and leaving the inbox,
/// archiving is leaving the inbox, reading is losing <c>UNREAD</c>. What this cannot read back is read-only, never dropped: a
/// filter that forwards, matches on size or uses a system label a rule does not have.
/// </summary>
internal static class GmailFilterMapper
{
    private const string Inbox = "INBOX";
    private const string Unread = "UNREAD";
    private const string Starred = "STARRED";
    private const string Trash = "TRASH";

    /// <summary>Gmail's ids for labels the user made; its system labels have names like <c>IMPORTANT</c>.</summary>
    private const string UserLabelPrefix = "Label_";

    public static object ToPayload(IReadOnlyCollection<MailRuleCondition> conditions, IReadOnlyCollection<RemoteRuleAction> actions)
    {
        var criteria = new Dictionary<string, object>();
        foreach (var condition in conditions)
        {
            switch (condition.Kind)
            {
                case RuleConditionKinds.From:
                    criteria["from"] = condition.Value;
                    break;
                case RuleConditionKinds.To:
                    criteria["to"] = condition.Value;
                    break;
                case RuleConditionKinds.Subject:
                    criteria["subject"] = condition.Value;
                    break;
                case RuleConditionKinds.HasWords or RuleConditionKinds.ProviderQuery:
                    criteria["query"] = condition.Value;
                    break;
                case RuleConditionKinds.HasAttachment:
                    criteria["hasAttachment"] = true;
                    break;
            }
        }

        var add = new List<string>();
        var remove = new List<string>();
        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case RuleActionKinds.AddLabel:
                    add.Add(action.ProviderLabelId!);
                    break;
                case RuleActionKinds.Move:
                    add.Add(action.ProviderLabelId!);
                    remove.Add(Inbox);
                    break;
                case RuleActionKinds.Archive:
                    remove.Add(Inbox);
                    break;
                case RuleActionKinds.MarkRead:
                    remove.Add(Unread);
                    break;
                case RuleActionKinds.Star:
                    add.Add(Starred);
                    break;
                case RuleActionKinds.Trash:
                    add.Add(Trash);
                    break;
            }
        }

        return new { criteria, action = new { addLabelIds = add.Distinct(), removeLabelIds = remove.Distinct() } };
    }

    public static RemoteFilter ToRemote(JsonElement filter)
    {
        var id = filter.GetProperty("id").GetString()!;
        var raw = filter.GetRawText();

        var conditions = ReadConditions(filter);
        var actions = ReadActions(filter);

        return conditions is { Count: > 0 } && actions is { Count: > 0 }
            ? new RemoteFilter(id, conditions, actions, raw, IsReadOnly: false)
            : new RemoteFilter(id, [], [], raw, IsReadOnly: true);
    }

    /// <summary>Null when the filter matches on something a rule cannot hold.</summary>
    private static List<MailRuleCondition>? ReadConditions(JsonElement filter)
    {
        if (!filter.TryGetProperty("criteria", out var criteria))
            return null;

        var conditions = new List<MailRuleCondition>();
        foreach (var property in criteria.EnumerateObject())
        {
            // A false flag is the same as no flag.
            if (property.Value.ValueKind == JsonValueKind.False)
                continue;

            switch (property.Name)
            {
                case "from":
                    conditions.Add(new MailRuleCondition(RuleConditionKinds.From, property.Value.GetString()!));
                    break;
                case "to":
                    conditions.Add(new MailRuleCondition(RuleConditionKinds.To, property.Value.GetString()!));
                    break;
                case "subject":
                    conditions.Add(new MailRuleCondition(RuleConditionKinds.Subject, property.Value.GetString()!));
                    break;
                case "query":
                    var query = property.Value.GetString()!;

                    // Both are Gmail's "has the words". With an operator (larger:5M) it is its search language.
                    conditions.Add(new MailRuleCondition(
                        query.Contains(':', StringComparison.Ordinal) ? RuleConditionKinds.ProviderQuery : RuleConditionKinds.HasWords, query));
                    break;
                case "hasAttachment":
                    conditions.Add(new MailRuleCondition(RuleConditionKinds.HasAttachment, string.Empty));
                    break;
                default:
                    return null;
            }
        }

        return conditions;
    }

    /// <summary>Null when the filter does something a rule cannot hold.</summary>
    private static List<RemoteRuleAction>? ReadActions(JsonElement filter)
    {
        if (!filter.TryGetProperty("action", out var action))
            return null;

        List<string> add = [];
        List<string> remove = [];
        foreach (var property in action.EnumerateObject())
        {
            switch (property.Name)
            {
                case "addLabelIds":
                    add = property.Value.EnumerateArray().Select(v => v.GetString()!).ToList();
                    break;
                case "removeLabelIds":
                    remove = property.Value.EnumerateArray().Select(v => v.GetString()!).ToList();
                    break;
                default:
                    return null;
            }
        }

        var userLabels = add.Where(l => l.StartsWith(UserLabelPrefix, StringComparison.Ordinal)).ToList();
        if (userLabels.Count > 1
            || add.Except(userLabels).Except([Starred, Trash]).Any()
            || remove.Except([Inbox, Unread]).Any())
            return null;

        var leavesInbox = remove.Contains(Inbox);
        var actions = new List<RemoteRuleAction>();
        if (userLabels.Count == 1)
            actions.Add(new RemoteRuleAction(leavesInbox ? RuleActionKinds.Move : RuleActionKinds.AddLabel, userLabels[0]));
        else if (leavesInbox)
            actions.Add(new RemoteRuleAction(RuleActionKinds.Archive, null));

        if (remove.Contains(Unread))
            actions.Add(new RemoteRuleAction(RuleActionKinds.MarkRead, null));
        if (add.Contains(Starred))
            actions.Add(new RemoteRuleAction(RuleActionKinds.Star, null));
        if (add.Contains(Trash))
            actions.Add(new RemoteRuleAction(RuleActionKinds.Trash, null));

        return actions;
    }
}
