using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ApplyMessageAction;

/// <param name="Action">One of <see cref="MessageActions"/>.</param>
/// <param name="LabelId">The label of <c>move</c>, <c>add-label</c> and <c>remove-label</c>.</param>
public sealed record ApplyMessageActionInput(
    Guid UserId,
    Guid MailboxId,
    IReadOnlyList<Guid> MessageIds,
    string? Action,
    Guid? LabelId);

public static class MessageActions
{
    public const string MarkRead = "mark-read";
    public const string MarkUnread = "mark-unread";
    public const string Star = "star";
    public const string Unstar = "unstar";
    public const string Move = "move";
    public const string Archive = "archive";
    public const string Trash = "trash";
    public const string AddLabel = "add-label";
    public const string RemoveLabel = "remove-label";

    public const int MaxMessages = 100;
}

/// <summary>
/// One action on up to <see cref="MessageActions.MaxMessages"/> messages of one mailbox. The provider first, the
/// local rows after it accepts: a failure changes nothing here. Returns the messages as they are now.
/// </summary>
public sealed class ApplyMessageActionCommand(ApplyMessageActionInput input)
    : CommandBase<ApplyMessageActionInput, IReadOnlyList<MessageSummaryDto>>(input);
