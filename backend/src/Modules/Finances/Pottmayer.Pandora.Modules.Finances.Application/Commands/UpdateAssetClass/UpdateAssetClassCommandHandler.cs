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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAssetClass;

public sealed class UpdateAssetClassCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateAssetClassCommand, AssetClassDto>
{
    protected override async Task<Result<AssetClassDto>> HandleAsync(UpdateAssetClassCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name)) return Fail(AssetErrors.InvalidName);

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var classes = ctx.AcquireRepository<IUserAssetClassRepository>();
            var assetClass = await classes.FindByIdForUserAsync(input.AssetClassId, input.UserId, token);
            if (assetClass is null)
                return Result<UserAssetClass>.Failure([await ctx.AcquireRepository<ISystemAssetClassRepository>().FindAsync(input.AssetClassId, token) is null
                    ? AssetErrors.ClassNotFound
                    : AssetErrors.ClassReadOnly]);
            if (await classes.ExistsWithNameAsync(input.UserId, input.Name, assetClass.Id, token))
                return Result<UserAssetClass>.Failure([AssetErrors.ClassNameAlreadyExists]);

            var old = assetClass.Name;
            if (!assetClass.Rename(input.Name)) return Result<UserAssetClass>.Failure([AssetErrors.ClassArchived]);
            await classes.UpdateAsync(assetClass, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetClassEvents.EntityType, assetClass.Id, AssetClassEvents.Renamed,
                timeProvider.GetUtcNow(), new { name = new { old, @new = assetClass.Name } }, ct: token);
            return Result<UserAssetClass>.Success(assetClass);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(AssetClassDto.From(result.Value!));
    }
}
