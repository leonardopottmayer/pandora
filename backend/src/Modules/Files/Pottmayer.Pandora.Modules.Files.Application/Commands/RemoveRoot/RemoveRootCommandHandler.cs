using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.RemoveRoot;

/// <summary>Stops scanning the root. Nothing is deleted: its entries become excluded and wait in the review inbox.</summary>
public sealed class RemoveRootCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<RemoveRootCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(RemoveRootCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<bool>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var root = await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(input.RootId, input.UserId, token);
            if (root is not { IsActive: true }) return FilesErrors.RootNotFound;

            root.Remove();
            await ctx.AcquireRepository<IEntryRepository>().ExcludeRootAsync(root.Id, timeProvider.GetUtcNow(), token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);
    }
}
