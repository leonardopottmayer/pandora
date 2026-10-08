using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.DecideReview;

/// <summary>
/// Forget removes entries from the catalog — the only way an entry ever leaves it. Keep takes them out of
/// the inbox and leaves them in the catalog as missing or excluded. Only entries waiting for review are
/// touched; a present one is never deleted from here.
/// </summary>
public sealed class DecideReviewCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DecideReviewCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(DecideReviewCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var folders = new List<(Guid, string)>();
        foreach (var folder in input.Folders ?? [])
        {
            if (!RootRules.TryNormalize(folder.Path, out var path)) return Fail(FilesErrors.InvalidPath(folder.Path));
            folders.Add((folder.RootId, path));
        }

        var touched = await factory.ExecuteAsync(FilesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IEntryRepository>().DecideReviewAsync(
                input.UserId, input.Decision, input.EntryIds ?? [], folders, timeProvider.GetUtcNow(), token),
            cancellationToken: ct);

        return Ok(touched);
    }
}
