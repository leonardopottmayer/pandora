using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAssetClass;

public sealed record UpdateAssetClassInput(Guid UserId, Guid AssetClassId, string Name);

/// <summary>Renames one of the user's own asset classes; system classes are read-only.</summary>
[RequiresPermission(FinancesPermissions.Investments.Update)]
public sealed class UpdateAssetClassCommand(UpdateAssetClassInput input) : CommandBase<UpdateAssetClassInput, AssetClassDto>(input);
