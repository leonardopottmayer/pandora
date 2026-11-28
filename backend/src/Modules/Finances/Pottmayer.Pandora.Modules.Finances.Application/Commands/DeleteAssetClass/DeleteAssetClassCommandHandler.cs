using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAssetClass;

public sealed class DeleteAssetClassCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteAssetClassCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteAssetClassCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var classes = ctx.AcquireRepository<IUserAssetClassRepository>();
            var assetClass = await classes.FindByIdForUserAsync(input.AssetClassId, input.UserId, token);
            if (assetClass is null)
                return Result<bool>.Failure([await ctx.AcquireRepository<ISystemAssetClassRepository>().FindAsync(input.AssetClassId, token) is null
                    ? AssetErrors.ClassNotFound
                    : AssetErrors.ClassReadOnly]);

            if (await ctx.AcquireRepository<IAssetRepository>().AnyForClassAsync(assetClass.Id, token))
                return Result<bool>.Failure([AssetErrors.ClassInUse]);

            await classes.RemoveAsync(assetClass, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetClassEvents.EntityType, assetClass.Id, AssetClassEvents.Deleted,
                timeProvider.GetUtcNow(), new { name = assetClass.Name }, ct: token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
