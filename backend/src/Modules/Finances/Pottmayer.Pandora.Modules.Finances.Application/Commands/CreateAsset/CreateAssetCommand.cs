using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAsset;

public sealed record CreateAssetInput(
    Guid UserId, Guid AssetClassId, string Name, string? Code, string? Isin, string? Issuer, string Currency);

/// <summary>Registers an asset the user owns or will own.</summary>
[RequiresPermission(FinancesPermissions.Investments.Create)]
public sealed class CreateAssetCommand(CreateAssetInput input) : CommandBase<CreateAssetInput, AssetDto>(input);
