using System.Security.Cryptography;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadImportFile;

public sealed class UploadImportFileCommandHandler(
    IUnitOfWorkFactory factory,
    IImportLayoutResolver layoutResolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<UploadImportFileCommand, ImportFileDto>
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    protected override async Task<Result<ImportFileDto>> HandleAsync(
        UploadImportFileCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.FileContent.Length > MaxFileSizeBytes)
            return Fail([ImportErrors.FileTooLarge]);

        // Exactly one destination must be set: an account import and a card import are mutually exclusive.
        if (input.AccountId is null && input.CardId is null)
            return Fail([ImportErrors.InvalidDestination]);
        if (input.AccountId is not null && input.CardId is not null)
            return Fail([ImportErrors.InvalidDestination]);

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var layoutRepo = ctx.AcquireRepository<IImportLayoutRepository>();
            var systemLayouts = await layoutRepo.GetSystemLayoutsAsync(token);

            // The layout is routed by the destination's bank + file format + account/card type. Only
            // when the destination has no bank (or no matching layout) does it fall back to sniffing
            // the file content. The bank comes from the account the user picked — or, for a card
            // import, from the account the card belongs to.
            string? bankCode = null;
            var accountRepo = ctx.AcquireRepository<IAccountRepository>();
            if (input.AccountId is not null)
            {
                var account = await accountRepo.FindByIdForUserAsync(input.AccountId.Value, input.UserId, token);
                bankCode = account?.BankCode;
            }
            else if (input.CardId is not null)
            {
                var card = await ctx.AcquireRepository<ICardRepository>()
                    .FindByIdForUserAsync(input.CardId.Value, input.UserId, token);
                if (card is not null)
                {
                    var account = await accountRepo.FindByIdForUserAsync(card.AccountId, input.UserId, token);
                    bankCode = account?.BankCode;
                }
            }

            var detectResult = await layoutResolver.ResolveAsync(
                input.FileContent, input.FileName, bankCode, input.CardId is not null, systemLayouts, token);

            if (detectResult.IsFailure)
                return Result<ImportFile>.Failure([ImportErrors.LayoutNotDetected]);

            var layout = detectResult.Value!;

            // The detected layout's own account type must agree with the destination the user picked
            // (a card statement layout can't be imported into a plain account, and vice versa).
            if (layout.IsCardLayout && input.AccountId is not null)
                return Result<ImportFile>.Failure([ImportErrors.InvalidDestination]);
            if (!layout.IsCardLayout && input.CardId is not null)
                return Result<ImportFile>.Failure([ImportErrors.InvalidDestination]);

            // The hash is what the dedup pipeline later uses to recognize a re-uploaded file.
            var fileHash = Convert.ToHexString(SHA256.HashData(input.FileContent)).ToLowerInvariant();

            var file = ImportFile.Create(
                input.UserId,
                layout.Id,
                input.AccountId,
                input.CardId,
                input.FileName,
                fileHash,
                input.FileContent,
                input.CutoffDate,
                timeProvider);

            var fileRepo = ctx.AcquireRepository<IImportFileRepository>();
            await fileRepo.AddAsync(file, token);

            return Result<ImportFile>.Success(file);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(ImportFileDto.From(result.Value!));
    }
}
