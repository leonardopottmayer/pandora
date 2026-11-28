using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAssetClass;

public sealed record DeleteAssetClassInput(Guid UserId, Guid AssetClassId);

/// <summary>Permanently removes one of the user's own classes. Fails while any asset, archived or not, uses it.</summary>
[RequiresPermission(FinancesPermissions.Investments.Delete)]
public sealed class DeleteAssetClassCommand(DeleteAssetClassInput input) : CommandBase<DeleteAssetClassInput, bool>(input);
