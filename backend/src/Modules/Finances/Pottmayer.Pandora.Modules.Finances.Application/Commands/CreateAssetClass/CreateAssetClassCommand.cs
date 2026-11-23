using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAssetClass;

public sealed record CreateAssetClassInput(Guid UserId, string Name, string Behavior);

/// <summary>Creates one of the user's own asset classes.</summary>
[RequiresPermission(FinancesPermissions.Investments.Create)]
public sealed class CreateAssetClassCommand(CreateAssetClassInput input) : CommandBase<CreateAssetClassInput, AssetClassDto>(input);
