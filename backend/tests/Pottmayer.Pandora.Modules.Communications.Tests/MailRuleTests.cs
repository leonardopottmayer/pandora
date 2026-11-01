using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Services;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

public sealed class MailRuleTests
{
    private static readonly Guid Mailbox = Guid.NewGuid();
    private static readonly MailLabel LabelA = MailLabel.Create(Mailbox, "a", "A", LabelKind.User, null, isExclusive: true);
    private static readonly MailLabel LabelB = MailLabel.Create(Mailbox, "b", "B", LabelKind.User, null, isExclusive: true);
    private static readonly Dictionary<Guid, MailLabel> Labels = new() { [LabelA.Id] = LabelA, [LabelB.Id] = LabelB };

    private static readonly MailRuleCondition FromAna = new(RuleConditionKinds.From, "ana");
    private static readonly MailRuleAction Read = new(RuleActionKinds.MarkRead, null);

    private static Error? Check(MailRuleCondition[] conditions, MailRuleAction[] actions, MailProvider? provider = null, string? name = "Rule") =>
        MailRule.Validate(name, conditions, actions, provider ?? MailProvider.Titan, Labels);

    [Fact]
    public void A_good_rule_passes() => Assert.Null(Check([FromAna], [Read]));

    [Fact]
    public void A_rule_with_no_condition_is_refused_since_it_would_match_all_mail() =>
        Assert.Equal(MailErrors.RuleConditionsInvalid.Code, Check([], [new MailRuleAction(RuleActionKinds.Trash, null)])!.Code);

    [Fact]
    public void A_condition_with_an_empty_value_is_refused_since_it_would_match_all_mail() =>
        Assert.Equal(MailErrors.RuleConditionsInvalid.Code, Check([new MailRuleCondition(RuleConditionKinds.From, "  ")], [Read])!.Code);

    [Fact]
    public void Has_attachment_needs_no_value() =>
        Assert.Null(Check([new MailRuleCondition(RuleConditionKinds.HasAttachment, "")], [Read]));

    [Fact]
    public void A_rule_with_no_action_is_refused() =>
        Assert.Equal(MailErrors.RuleActionsInvalid.Code, Check([FromAna], [])!.Code);

    [Fact]
    public void Unknown_kinds_are_refused()
    {
        Assert.Equal(MailErrors.RuleConditionsInvalid.Code, Check([new MailRuleCondition("nonsense", "x")], [Read])!.Code);
        Assert.Equal(MailErrors.RuleActionsInvalid.Code, Check([FromAna], [new MailRuleAction("delete-forever", null)])!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_rule_needs_a_name(string? name) =>
        Assert.Equal(MailErrors.RuleNameInvalid.Code, Check([FromAna], [Read], name: name)!.Code);

    [Fact]
    public void A_name_over_the_limit_is_refused() =>
        Assert.Equal(MailErrors.RuleNameInvalid.Code, Check([FromAna], [Read], name: new string('x', 101))!.Code);

    [Fact]
    public void A_label_that_does_not_exist_is_refused() =>
        Assert.Equal(MailErrors.RuleActionsInvalid.Code, Check([FromAna], [new MailRuleAction(RuleActionKinds.Move, Guid.NewGuid())])!.Code);

    [Fact]
    public void A_move_needs_a_label() =>
        Assert.Equal(MailErrors.RuleActionsInvalid.Code, Check([FromAna], [new MailRuleAction(RuleActionKinds.Move, null)])!.Code);

    [Fact]
    public void Gmail_takes_one_user_label_per_filter()
    {
        var one = new MailRuleAction(RuleActionKinds.AddLabel, LabelA.Id);
        var other = new MailRuleAction(RuleActionKinds.AddLabel, LabelB.Id);

        Assert.Null(Check([FromAna], [one], MailProvider.Gmail));
        Assert.Equal(MailErrors.RuleActionsInvalid.Code, Check([FromAna], [one, other], MailProvider.Gmail)!.Code);
    }

    [Fact]
    public void Gmail_takes_only_a_user_label_while_imap_can_move_to_a_system_folder()
    {
        var system = MailLabel.Create(Mailbox, "IMPORTANT", "Important", LabelKind.System, null, isExclusive: false);
        var labels = new Dictionary<Guid, MailLabel> { [system.Id] = system };
        MailRuleAction[] move = [new(RuleActionKinds.Move, system.Id)];

        Assert.Equal(MailErrors.RuleActionsInvalid.Code, MailRule.Validate("Rule", [FromAna], move, MailProvider.Gmail, labels)!.Code);
        Assert.Null(MailRule.Validate("Rule", [FromAna], move, MailProvider.Titan, labels));
    }

    [Fact]
    public void Gmail_has_one_field_per_condition_while_imap_can_ask_for_both()
    {
        var two = new[] { FromAna, new MailRuleCondition(RuleConditionKinds.From, "bo") };
        var query = new[] { new MailRuleCondition(RuleConditionKinds.HasWords, "x"), new MailRuleCondition(RuleConditionKinds.ProviderQuery, "y") };

        Assert.Equal(MailErrors.RuleConditionsInvalid.Code, Check(two, [Read], MailProvider.Gmail)!.Code);
        Assert.Equal(MailErrors.RuleConditionsInvalid.Code, Check(query, [Read], MailProvider.Gmail)!.Code);
        Assert.Null(Check(two, [Read]));
    }

    [Fact]
    public void Imap_cannot_add_a_label_only_move() =>
        Assert.Equal(MailErrors.RuleActionsInvalid.Code, Check([FromAna], [new MailRuleAction(RuleActionKinds.AddLabel, LabelA.Id)])!.Code);

    [Fact]
    public void A_provider_query_is_for_gmail_only()
    {
        var query = new MailRuleCondition(RuleConditionKinds.ProviderQuery, "larger:5M");

        Assert.Null(Check([query], [Read], MailProvider.Gmail));
        Assert.Equal(MailErrors.RuleConditionsInvalid.Code, Check([query], [Read])!.Code);
    }

    [Theory]
    [InlineData(RuleConditionKinds.From, "ana", true)]
    [InlineData(RuleConditionKinds.From, "ANA@MAIL", true)]
    [InlineData(RuleConditionKinds.From, "Ana Souza", true)]
    [InlineData(RuleConditionKinds.From, "bo", false)]
    [InlineData(RuleConditionKinds.To, "me@titan", true)]
    [InlineData(RuleConditionKinds.To, "cc@mail", true)]
    [InlineData(RuleConditionKinds.To, "ana", false)]
    [InlineData(RuleConditionKinds.Subject, "weekly   DIGEST", true)]
    [InlineData(RuleConditionKinds.Subject, "monthly", false)]
    [InlineData(RuleConditionKinds.HasWords, "invoice", true)]
    [InlineData(RuleConditionKinds.HasWords, "digest", true)]
    [InlineData(RuleConditionKinds.HasWords, "absent", false)]
    [InlineData(RuleConditionKinds.ProviderQuery, "weekly", false)]
    public void Conditions_match_as_gmail_treats_them(string kind, string value, bool expected) =>
        Assert.Equal(expected, RuleMatcher.Matches(Rule(new MailRuleCondition(kind, value)), Message(), "Your invoice is here"));

    [Fact]
    public void Has_attachment_looks_at_the_flag()
    {
        var rule = Rule(new MailRuleCondition(RuleConditionKinds.HasAttachment, ""));

        Assert.True(RuleMatcher.Matches(rule, Message(hasAttachments: true), null));
        Assert.False(RuleMatcher.Matches(rule, Message(hasAttachments: false), null));
    }

    [Fact]
    public void Every_condition_must_match()
    {
        var both = Rule(FromAna, new MailRuleCondition(RuleConditionKinds.Subject, "digest"));
        var one = Rule(FromAna, new MailRuleCondition(RuleConditionKinds.Subject, "monthly"));

        Assert.True(RuleMatcher.Matches(both, Message(), null));
        Assert.False(RuleMatcher.Matches(one, Message(), null));
    }

    [Fact]
    public void A_rule_with_no_conditions_never_matches() =>
        Assert.False(RuleMatcher.Matches(Rule(), Message(), null));

    [Fact]
    public void The_text_is_needed_only_when_it_can_still_decide()
    {
        var words = new MailRuleCondition(RuleConditionKinds.HasWords, "invoice");

        Assert.False(RuleMatcher.NeedsText(Rule(FromAna), Message()));
        Assert.True(RuleMatcher.NeedsText(Rule(FromAna, words), Message()));

        // The subject already has the words.
        Assert.False(RuleMatcher.NeedsText(Rule(new MailRuleCondition(RuleConditionKinds.HasWords, "digest")), Message()));

        // Another condition already failed.
        Assert.False(RuleMatcher.NeedsText(Rule(new MailRuleCondition(RuleConditionKinds.From, "bo"), words), Message()));
    }

    [Fact]
    public void Flags_and_labels_add_up()
    {
        var plan = RuleMatcher.Plan([
            Rule(1, new MailRuleAction(RuleActionKinds.MarkRead, null), new MailRuleAction(RuleActionKinds.AddLabel, LabelA.Id)),
            Rule(2, new MailRuleAction(RuleActionKinds.Star, null), new MailRuleAction(RuleActionKinds.AddLabel, LabelB.Id)),
            Rule(3, new MailRuleAction(RuleActionKinds.AddLabel, LabelA.Id)),
        ]);

        Assert.True(plan.MarkRead);
        Assert.True(plan.Star);
        Assert.Equal([LabelA.Id, LabelB.Id], plan.AddLabels);
        Assert.False(plan.IsEmpty);
    }

    [Fact]
    public void The_first_move_by_position_wins_whatever_the_order_given()
    {
        var plan = RuleMatcher.Plan([
            Rule(2, new MailRuleAction(RuleActionKinds.Move, LabelB.Id)),
            Rule(1, new MailRuleAction(RuleActionKinds.Move, LabelA.Id)),
        ]);

        Assert.Equal(LabelA.Id, plan.MoveTo);
    }

    [Fact]
    public void Trash_beats_a_move_and_an_archive_but_flags_still_apply()
    {
        var plan = RuleMatcher.Plan([
            Rule(1, new MailRuleAction(RuleActionKinds.Move, LabelA.Id), new MailRuleAction(RuleActionKinds.MarkRead, null)),
            Rule(2, new MailRuleAction(RuleActionKinds.Archive, null)),
            Rule(3, new MailRuleAction(RuleActionKinds.Trash, null)),
        ]);

        Assert.True(plan.Trash);
        Assert.Null(plan.MoveTo);
        Assert.False(plan.Archive);
        Assert.True(plan.MarkRead);
    }

    [Fact]
    public void A_move_beats_an_archive()
    {
        var plan = RuleMatcher.Plan([
            Rule(1, new MailRuleAction(RuleActionKinds.Archive, null)),
            Rule(2, new MailRuleAction(RuleActionKinds.Move, LabelA.Id)),
        ]);

        Assert.Equal(LabelA.Id, plan.MoveTo);
        Assert.False(plan.Archive);
    }

    [Fact]
    public void No_matching_rules_is_an_empty_plan() => Assert.True(RuleMatcher.Plan([]).IsEmpty);

    [Fact]
    public void Only_an_enabled_healthy_pandora_rule_runs()
    {
        var rule = Rule(FromAna);
        Assert.True(rule.RunsInPandora);

        rule.SetEnabled(false);
        Assert.False(rule.RunsInPandora);
        rule.SetEnabled(true);

        rule.MarkBroken("The label is gone.");
        Assert.False(rule.RunsInPandora);
        Assert.Equal("The label is gone.", rule.LastError);

        rule.Update("Fixed", [FromAna], [Read]);
        Assert.True(rule.RunsInPandora);
        Assert.Null(rule.LastError);

        var filter = MailRule.Create(Mailbox, "Filter", 0, [FromAna], [Read], RuleExecution.ProviderFilter);
        Assert.False(filter.RunsInPandora);
    }

    [Fact]
    public void A_read_only_rule_keeps_the_filter_as_sent_and_never_breaks()
    {
        var rule = MailRule.ImportReadOnly(Mailbox, "Forward to bo", 0, "f1", """{"id":"f1"}""");

        Assert.True(rule.IsReadOnly);
        Assert.Equal("""{"id":"f1"}""", rule.ProviderFilterRaw);
        Assert.Equal("f1", rule.ProviderFilterId);

        rule.MarkBroken("nope");
        Assert.True(rule.IsReadOnly);
        Assert.Null(rule.LastError);
        Assert.False(rule.RunsInPandora);
    }

    private static MailRule Rule(params MailRuleCondition[] conditions) =>
        MailRule.Create(Mailbox, "Rule", 0, conditions, [Read], RuleExecution.Pandora);

    private static MailRule Rule(int position, params MailRuleAction[] actions) =>
        MailRule.Create(Mailbox, "Rule", position, [FromAna], actions, RuleExecution.Pandora);

    private static MailMessage Message(bool hasAttachments = false) =>
        MailMessage.Create(
            Mailbox, "m1", "t1", null, new MailAddress("Ana  Souza", "ana@mail.test"),
            [new MailAddress(null, "me@titan.test")], [new MailAddress("Copy", "cc@mail.test")],
            "Weekly digest", "", DateTimeOffset.UtcNow, isUnread: true, isStarred: false, hasAttachments, 100, []);
}
