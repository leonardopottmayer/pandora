using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAsset;

public sealed record DeleteAssetInput(Guid UserId, Guid AssetId);

/// <summary>Permanently removes an asset registered by mistake. Fails once it has any position, open or closed.</summary>
[RequiresPermission(FinancesPermissions.Investments.Delete)]
public sealed class DeleteAssetCommand(DeleteAssetInput input) : CommandBase<DeleteAssetInput, bool>(input);
