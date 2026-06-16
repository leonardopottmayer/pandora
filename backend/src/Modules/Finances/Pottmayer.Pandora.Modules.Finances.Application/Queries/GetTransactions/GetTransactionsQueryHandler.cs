using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Localization.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransactions;

public sealed class GetTransactionsQueryHandler(IUnitOfWorkFactory factory, IMessageProvider messages)
    : QueryHandlerBase<GetTransactionsQuery, IReadOnlyList<TransactionDto>>
{
    private const int MaxTake = 200;

    protected override async Task<Result<IReadOnlyList<TransactionDto>>> HandleAsync(
        GetTransactionsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var take = input.Take is <= 0 or > MaxTake ? 50 : input.Take;
        var skip = input.Skip < 0 ? 0 : input.Skip;

        var tagIds = input.TagIds?.Distinct().ToList();

        var queryResult = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            // OR semantics: a transaction matches if it carries any of the requested tags. We resolve
            // the matching transaction ids first, then constrain the page query — paging stays intact.
            IReadOnlyCollection<Guid>? ids = null;
            if (tagIds is { Count: > 0 })
                ids = await ctx.AcquireRepository<ITagLinkRepository>()
                    .GetEntityIdsByTagsAsync(TaggableEntityType.Transaction, tagIds, token);

            var filter = new TransactionFilter(
                input.AccountId, null, input.From, input.To, input.Kind, input.Status,
                input.SystemCategoryId, input.UserCategoryId, input.Text, input.Origin, ids, skip, take);

            var txList = await ctx.AcquireRepository<ITransactionRepository>().QueryAsync(input.UserId, filter, token);

            var statementIds = txList
                .Where(t => t.CardStatementId.HasValue)
                .Select(t => t.CardStatementId!.Value)
                .Distinct()
                .ToList();
            var stmtMap = statementIds.Count > 0
                ? (await ctx.AcquireRepository<ICardStatementRepository>()
                    .GetByIdsAsync(statementIds, input.UserId, token))
                    .ToDictionary(s => s.Id)
                : [];

            return Result<(IReadOnlyList<Transaction>, Dictionary<Guid, CardStatement>)>.Success((txList, stmtMap));
        }, cancellationToken: ct);

        if (queryResult.IsFailure) return Fail([.. queryResult.Errors]);

        var (transactions, stmtsById) = queryResult.Value!;
        IEnumerable<TransactionDto> dtos = transactions.Select(t =>
        {
            var stmt = t.CardStatementId.HasValue && stmtsById.TryGetValue(t.CardStatementId.Value, out var s) ? s : null;
            return TransactionDto.From(t, messages, stmt);
        });

        if (!string.IsNullOrWhiteSpace(input.Text))
        {
            // The repository couldn't filter/page system-described entries (e.g. "Saldo inicial",
            // "Pagamento da fatura ...") in SQL since their text is only rendered here — re-check the
            // rendered description and apply paging now that the set is final.
            var text = input.Text.Trim();
            dtos = dtos
                .Where(d =>
                    d.Description.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    (d.Payee?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
                .Skip(skip)
                .Take(take);
        }

        return Ok((IReadOnlyList<TransactionDto>)[.. dtos]);
    }
}
