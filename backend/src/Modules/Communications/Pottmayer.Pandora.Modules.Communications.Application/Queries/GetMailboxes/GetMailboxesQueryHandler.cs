using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;

public sealed class GetMailboxesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetMailboxesQuery, IReadOnlyList<MailboxDto>>
{
    protected override async Task<Result<IReadOnlyList<MailboxDto>>> HandleAsync(GetMailboxesQuery request, CancellationToken ct)
    {
        var mailboxes = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var list = await ctx.AcquireRepository<IMailboxRepository>().ListByUserAsync(request.Input.UserId, token);
            var counts = await ctx.AcquireRepository<IMailMessageRepository>().CountByMailboxAsync([.. list.Select(m => m.Id)], token);
            return list.Select(m => MailboxDto.From(m, counts.GetValueOrDefault(m.Id))).ToList();
        }, cancellationToken: ct);

        return Ok(mailboxes);
    }
}
