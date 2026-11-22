using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetLabels;

public sealed class GetLabelsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetLabelsQuery, IReadOnlyList<LabelDto>>
{
    protected override async Task<Result<IReadOnlyList<LabelDto>>> HandleAsync(GetLabelsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        return await factory.ExecuteAsync<Result<IReadOnlyList<LabelDto>>>(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var mailbox = await ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(input.MailboxId, input.UserId, token);
            if (mailbox is null)
                return MailErrors.MailboxNotFound;

            var labels = await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, token);
            var counts = await ctx.AcquireRepository<IMailMessageRepository>().CountByLabelAsync(mailbox.Id, token);
            return Result<IReadOnlyList<LabelDto>>.Success(labels.OrderBy(l => l.Kind == LabelKind.System ? 0 : 1)
                         .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                         .Select(l => LabelDto.From(l, counts.GetValueOrDefault(l.Id)))
                         .ToList());
        }, cancellationToken: ct);
    }
}
