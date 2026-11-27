using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAssetArchived;

public sealed record SetAssetArchivedInput(Guid UserId, Guid AssetId, bool Archived);

/// <summary>Archives or brings back an asset. Positions already holding it are left as they are; an archived asset just can't open new ones.</summary>
[RequiresPermission(FinancesPermissions.Investments.Update)]
public sealed class SetAssetArchivedCommand(SetAssetArchivedInput input) : CommandBase<SetAssetArchivedInput, AssetDto>(input);
