using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.ListMessages;

/// <param name="MailboxId">Null lists every mailbox of the caller.</param>
/// <param name="Before">The previous page's <see cref="MessagePageDto.NextCursor"/>.</param>
public sealed record ListMessagesInput(
    Guid UserId,
    Guid? MailboxId,
    Guid? LabelId,
    bool? Unread,
    string? Search,
    string? ThreadKey,
    string? Before,
    int Limit);

/// <summary>Newest first, by keyset.</summary>
public sealed class ListMessagesQuery(ListMessagesInput input)
    : QueryBase<ListMessagesInput, MessagePageDto>(input);
