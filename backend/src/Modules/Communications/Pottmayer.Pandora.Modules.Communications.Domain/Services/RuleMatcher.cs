using Pottmayer.Pandora.Modules.Communications.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Services;

/// <summary>What the rules that match add up to for one message.</summary>
public sealed record RulePlan(bool Trash, Guid? MoveTo, bool Archive, bool MarkRead, bool Star, IReadOnlyList<Guid> AddLabels)
{
    public bool IsEmpty => !Trash && MoveTo is null && !Archive && !MarkRead && !Star && AddLabels.Count == 0;
}

/// <summary>
/// Whether a rule matches a message, the way Gmail treats the same conditions, so a rule behaves alike on both: substrings,
/// ignoring case and repeated spaces. Pure: the caller fetches the text, and only when <see cref="NeedsText"/> says so.
/// </summary>
public static class RuleMatcher
{
    public static bool NeedsText(MailRule rule) => rule.Conditions.Any(c => c.Kind == RuleConditionKinds.HasWords);

    public static bool Matches(MailRule rule, MailMessage message, string? text) =>
        rule.Conditions.Count > 0 && rule.Conditions.All(c => Matches(c, message, text));

    /// <summary>
    /// All the rules that match apply, in position order: flags and labels add up, trash beats a move, and the first move
    /// wins, since a message is in one folder.
    /// </summary>
    public static RulePlan Plan(IEnumerable<MailRule> matching)
    {
        var actions = matching.OrderBy(r => r.Position).SelectMany(r => r.Actions).ToList();
        var trash = actions.Any(a => a.Kind == RuleActionKinds.Trash);
        var moveTo = trash ? null : actions.FirstOrDefault(a => a.Kind == RuleActionKinds.Move)?.LabelId;

        return new RulePlan(
            trash,
            moveTo,
            Archive: !trash && moveTo is null && actions.Any(a => a.Kind == RuleActionKinds.Archive),
            MarkRead: actions.Any(a => a.Kind == RuleActionKinds.MarkRead),
            Star: actions.Any(a => a.Kind == RuleActionKinds.Star),
            AddLabels: actions.Where(a => a.Kind == RuleActionKinds.AddLabel && a.LabelId is not null)
                              .Select(a => a.LabelId!.Value)
                              .Distinct()
                              .ToList());
    }

    private static bool Matches(MailRuleCondition condition, MailMessage message, string? text) => condition.Kind switch
    {
        RuleConditionKinds.From => Has(message.FromName, condition.Value) || Has(message.FromAddress, condition.Value),
        RuleConditionKinds.To => message.To.Concat(message.Cc).Any(a => Has(a.Name, condition.Value) || Has(a.Address, condition.Value)),
        RuleConditionKinds.Subject => Has(message.Subject, condition.Value),
        RuleConditionKinds.HasWords => Has(message.Subject, condition.Value) || Has(text, condition.Value),
        RuleConditionKinds.HasAttachment => message.HasAttachments,

        // Gmail's own search language, which only Gmail can run; this engine never sees a Gmail rule.
        _ => false,
    };

    private static bool Has(string? haystack, string needle) =>
        haystack is not null && Collapse(haystack).Contains(Collapse(needle), StringComparison.OrdinalIgnoreCase);

    private static string Collapse(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
