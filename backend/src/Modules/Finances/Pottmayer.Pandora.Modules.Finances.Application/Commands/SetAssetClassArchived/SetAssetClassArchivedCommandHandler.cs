using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAssetClassArchived;

public sealed class SetAssetClassArchivedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetAssetClassArchivedCommand, AssetClassDto>
{
    protected override async Task<Result<AssetClassDto>> HandleAsync(SetAssetClassArchivedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var classes = ctx.AcquireRepository<IAssetClassRepository>();
            var assetClass = await classes.FindVisibleAsync(input.AssetClassId, input.UserId, token);
            if (assetClass is null) return Result<AssetClass>.Failure([AssetErrors.ClassNotFound]);

            var changed = input.Archived ? assetClass.Archive(timeProvider) : assetClass.Unarchive();
            if (!changed) return Result<AssetClass>.Failure([AssetErrors.ClassReadOnly]);
            await classes.UpdateAsync(assetClass, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetClassEvents.EntityType, assetClass.Id,
                input.Archived ? AssetClassEvents.Archived : AssetClassEvents.Unarchived, timeProvider.GetUtcNow(),
                new { name = assetClass.Name }, ct: token);
            return Result<AssetClass>.Success(assetClass);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(AssetClassDto.From(result.Value!));
    }
}
