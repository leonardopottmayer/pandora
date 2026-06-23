using System.Text.Json;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunImportParsing;

public sealed class RunImportParsingCommandHandler(
    IUnitOfWorkFactory factory,
    IEnumerable<IImportParser> parsers,
    IDuplicateDetector duplicateDetector,
    IStatementResolver statementResolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<RunImportParsingCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(
        RunImportParsingCommand request, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var fileRepo = ctx.AcquireRepository<IImportFileRepository>();
            var file = await fileRepo.ClaimNextReceivedAsync(token);
            if (file is null) return Result<bool>.Success(false);

            var started = file.StartParsing(timeProvider);
            if (!started) return Result<bool>.Success(false);
            await fileRepo.UpdateAsync(file, token);

            try
            {
                var layoutRepo = ctx.AcquireRepository<IImportLayoutRepository>();
                ImportLayout? layout = null;
                if (file.LayoutId.HasValue)
                    layout = await layoutRepo.GetByIdAsync(file.LayoutId.Value, token);

                if (layout is null)
                    throw new InvalidOperationException($"Layout not found for file {file.Id}.");

                var parser = parsers.FirstOrDefault(p => p.CanParse(layout))
                    ?? throw new InvalidOperationException($"No parser found for layout {layout.LayoutCode}.");

                var parsedRows = await parser.ParseAsync(file.FileContent, layout, token);

                var rowRepo = ctx.AcquireRepository<IImportRowRepository>();
                var pendingRepo = ctx.AcquireRepository<IPendingTransactionRepository>();
                var transactionRepo = ctx.AcquireRepository<ITransactionRepository>();
                var statementRepo = ctx.AcquireRepository<ICardStatementRepository>();
                var cardRepo = ctx.AcquireRepository<ICardRepository>();

                var dedupResults = await duplicateDetector.DetectAsync(
                    file.UserId, file.AccountId, file.CardId, parsedRows,
                    rowRepo, fileRepo, transactionRepo, pendingRepo, token);
                var dedupByIndex = dedupResults.ToDictionary(r => r.RowIndex);

                var now = timeProvider.GetUtcNow();
                int parsed = 0, errors = 0, duplicates = 0, suggestions = 0;

                foreach (var pr in parsedRows)
                {
                    var row = ImportRow.CreatePending(file.Id, pr.RowIndex, pr.RawData, now);

                    if (pr.ShouldSkip)
                    {
                        row.MarkSkipped();
                        await rowRepo.AddAsync(row, token);
                        continue;
                    }

                    try
                    {
                        var payload = SerializePayload(pr);
                        dedupByIndex.TryGetValue(pr.RowIndex, out var dedup);
                        var dedupKey = dedup?.DedupKey ?? string.Empty;
                        var dedupStatus = DedupStatus.FromValue(dedup?.DedupStatus ?? "new");

                        row.SetParsed(payload, pr.ExternalId, dedupKey, pr.InstallmentNumber, pr.InstallmentCount);
                        row.SetDedup(dedupStatus, dedup?.MatchedTransactionId, dedup?.MatchedPendingTransactionId);
                        await rowRepo.AddAsync(row, token);

                        if (dedupStatus == DedupStatus.Certain) duplicates++;

                        // Always generate a suggestion, even for certain duplicates
                        var kind = DetermineKind(pr.IsCredit, layout.IsCardLayout);
                        Guid? suggestedStatementId = null;

                        if (file.CardId.HasValue)
                        {
                            var card = await cardRepo.FindByIdForUserAsync(file.CardId.Value, file.UserId, token);
                            if (card is not null)
                            {
                                var stmtResult = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                                    statementRepo, statementResolver, card, file.UserId,
                                    pr.OccurredOn, forcedStatementId: null, timeProvider, token);
                                if (stmtResult.IsSuccess)
                                    suggestedStatementId = stmtResult.Value!.Statement.Id;
                            }
                        }

                        var pending = PendingTransaction.CreateFromImport(
                            file.UserId,
                            row.Id,
                            file.AccountId,
                            file.CardId,
                            kind,
                            pr.Amount,
                            pr.Currency,
                            pr.OccurredOn,
                            pr.Description,
                            pr.Payee,
                            suggestedStatementId,
                            dedupStatus,
                            dedup?.MatchedTransactionId,
                            dedup?.MatchedPendingTransactionId,
                            pr.InstallmentNumber,
                            pr.InstallmentCount,
                            payload,
                            timeProvider);

                        await pendingRepo.AddAsync(pending, token);
                        row.MarkSuggestionCreated(pending.Id);

                        parsed++;
                        suggestions++;
                    }
                    catch (Exception ex)
                    {
                        row.MarkError(ex.Message);
                        await rowRepo.AddAsync(row, token);
                        errors++;
                    }
                }

                file.Complete(parsedRows.Count, parsed, errors, duplicates, suggestions, timeProvider);
                await fileRepo.UpdateAsync(file, token);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                file.Fail(ex.Message, timeProvider);
                await fileRepo.UpdateAsync(file, token);
                return Result<bool>.Success(true);
            }
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }

    private static string DetermineKind(bool isCredit, bool isCardLayout)
    {
        // For card layouts: most transactions are expenses (debit); isCredit = payment/credit
        // For account layouts: isCredit = money coming in = income
        if (isCardLayout)
            return isCredit ? TransactionKind.Income.Value : TransactionKind.Expense.Value;
        return isCredit ? TransactionKind.Income.Value : TransactionKind.Expense.Value;
    }

    private static string SerializePayload(ParsedImportRow pr) =>
        JsonSerializer.Serialize(new
        {
            pr.OccurredOn,
            pr.Amount,
            pr.Currency,
            pr.Description,
            pr.Payee,
            pr.ExternalId,
            pr.InstallmentNumber,
            pr.InstallmentCount,
            pr.IsCredit
        });
}
