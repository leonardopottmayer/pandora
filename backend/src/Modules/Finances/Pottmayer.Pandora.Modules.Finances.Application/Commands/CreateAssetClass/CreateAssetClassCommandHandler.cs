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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAssetClass;

public sealed class CreateAssetClassCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateAssetClassCommand, AssetClassDto>
{
    protected override async Task<Result<AssetClassDto>> HandleAsync(CreateAssetClassCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name)) return Fail(AssetErrors.InvalidName);
        if (!AssetBehavior.IsSupported(input.Behavior)) return Fail(AssetErrors.InvalidBehavior(input.Behavior));

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var classes = ctx.AcquireRepository<IUserAssetClassRepository>();
            if (await classes.ExistsWithNameAsync(input.UserId, input.Name, null, token))
                return Result<UserAssetClass>.Failure([AssetErrors.ClassNameAlreadyExists]);

            var assetClass = UserAssetClass.Create(input.UserId, input.Name, AssetBehavior.FromValue(input.Behavior), timeProvider);
            await classes.AddAsync(assetClass, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetClassEvents.EntityType, assetClass.Id, AssetClassEvents.Created,
                timeProvider.GetUtcNow(), new { name = assetClass.Name, behavior = assetClass.Behavior.Value }, ct: token);
            return Result<UserAssetClass>.Success(assetClass);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(AssetClassDto.From(result.Value!));
    }
}
