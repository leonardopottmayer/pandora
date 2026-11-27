using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAssetClassArchived;

public sealed record SetAssetClassArchivedInput(Guid UserId, Guid AssetClassId, bool Archived);

/// <summary>Archives or brings back one of the user's own asset classes.</summary>
[RequiresPermission(FinancesPermissions.Investments.Update)]
public sealed class SetAssetClassArchivedCommand(SetAssetClassArchivedInput input) : CommandBase<SetAssetClassArchivedInput, AssetClassDto>(input);
