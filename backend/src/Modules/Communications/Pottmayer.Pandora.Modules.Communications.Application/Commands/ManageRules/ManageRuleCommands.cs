using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageRules;

public sealed record CreateRuleInput(
    Guid UserId, Guid MailboxId, string? Name, IReadOnlyCollection<MailRuleCondition> Conditions, IReadOnlyCollection<MailRuleAction> Actions);

public sealed record UpdateRuleInput(
    Guid UserId, Guid RuleId, string? Name, IReadOnlyCollection<MailRuleCondition> Conditions, IReadOnlyCollection<MailRuleAction> Actions, bool IsEnabled);

public sealed record DeleteRuleInput(Guid UserId, Guid RuleId);

public sealed record ReorderRulesInput(Guid UserId, Guid MailboxId, IReadOnlyList<Guid> RuleIds);

/// <summary>Returns the mailbox's rules as they are now.</summary>
public sealed class CreateRuleCommand(CreateRuleInput input) : CommandBase<CreateRuleInput, IReadOnlyList<RuleDto>>(input);

/// <summary>
/// Saves a rule, enabled or not. On Gmail, the filter is touched only when what it does changes: a new name is Pandora's
/// own. Disabling deletes the filter, enabling makes it again, and an edit is create, save, delete. If only that delete
/// fails, this succeeds with the rule's last error saying so. <inheritdoc cref="CreateRuleCommand"/>
/// </summary>
public sealed class UpdateRuleCommand(UpdateRuleInput input) : CommandBase<UpdateRuleInput, IReadOnlyList<RuleDto>>(input);

/// <summary>On Gmail, deletes the filter first, for good. <inheritdoc cref="CreateRuleCommand"/></summary>
public sealed class DeleteRuleCommand(DeleteRuleInput input) : CommandBase<DeleteRuleInput, IReadOnlyList<RuleDto>>(input);

/// <summary>
/// The ones listed, in that order, then any other, such as a filter imported while the screen was open.
/// <inheritdoc cref="CreateRuleCommand"/>
/// </summary>
public sealed class ReorderRulesCommand(ReorderRulesInput input) : CommandBase<ReorderRulesInput, IReadOnlyList<RuleDto>>(input);
