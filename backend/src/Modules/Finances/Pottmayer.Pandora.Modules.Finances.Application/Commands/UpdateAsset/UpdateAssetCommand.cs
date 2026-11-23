using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAsset;

public sealed record UpdateAssetInput(Guid UserId, Guid AssetId, string Name, string? Code, string? Isin, string? Issuer);

/// <summary>Edits an asset; its class and currency are fixed, since positions depend on them.</summary>
[RequiresPermission(FinancesPermissions.Investments.Update)]
public sealed class UpdateAssetCommand(UpdateAssetInput input) : CommandBase<UpdateAssetInput, AssetDto>(input);
