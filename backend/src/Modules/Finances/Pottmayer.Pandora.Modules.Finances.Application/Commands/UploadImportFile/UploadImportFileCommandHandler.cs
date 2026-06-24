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
    ILayoutDetector layoutDetector,
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

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            // The layout is inferred from the file's own content/headers rather than chosen by the
            // user — they only pick the destination (account or card).
            var layoutRepo = ctx.AcquireRepository<IImportLayoutRepository>();
            var systemLayouts = await layoutRepo.GetSystemLayoutsAsync(token);

            var detectResult = await layoutDetector.DetectAsync(
                input.FileContent, input.FileName, systemLayouts, token);

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
                timeProvider);

            var fileRepo = ctx.AcquireRepository<IImportFileRepository>();
            await fileRepo.AddAsync(file, token);

            return Result<ImportFile>.Success(file);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(ImportFileDto.From(result.Value!));
    }
}
