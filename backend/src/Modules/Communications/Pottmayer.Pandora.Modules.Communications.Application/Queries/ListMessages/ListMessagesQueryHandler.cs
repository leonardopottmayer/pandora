using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.ListMessages;

public sealed class ListMessagesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<ListMessagesQuery, MessagePageDto>
{
    public const int MaxLimit = 100;

    protected override async Task<Result<MessagePageDto>> HandleAsync(ListMessagesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        (DateTimeOffset, Guid)? before = null;
        if (input.Before is not null)
        {
            if (!MessageCursor.TryDecode(input.Before, out var decoded))
                return Fail(MailErrors.InvalidCursor);
            before = decoded;
        }

        var limit = Math.Clamp(input.Limit, 1, MaxLimit);

        return await factory.ExecuteAsync<Result<MessagePageDto>>(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var mailboxIds = (await ctx.AcquireRepository<IMailboxRepository>().ListByUserAsync(input.UserId, token))
                .Select(m => m.Id)
                .ToList();
            if (input.MailboxId is { } mailboxId)
            {
                if (!mailboxIds.Contains(mailboxId))
                    return MailErrors.MailboxNotFound;
                mailboxIds = [mailboxId];
            }

            // One row more than the page tells whether another page follows.
            var rows = await ctx.AcquireRepository<IMailMessageRepository>().ListPageAsync(new MailMessageQuery(
                mailboxIds, input.LabelId, input.Unread, input.Search, input.ThreadKey, before, limit + 1), token);

            var page = rows.Take(limit).ToList();
            var next = rows.Count > limit ? MessageCursor.Encode(page[^1]) : null;
            return Result<MessagePageDto>.Success(new MessagePageDto([.. page.Select(MessageSummaryDto.FromMessage)], next));
        }, cancellationToken: ct);
    }
}
