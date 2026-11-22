using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTag;

public sealed record UpdateTagInput(Guid UserId, Guid TagId, string Name, string? Color);

/// <summary>Renames a tag and/or changes its color. Applies retroactively everywhere it's already linked.</summary>
[RequiresPermission(FinancesPermissions.Tags.Update)]
public sealed class UpdateTagCommand(UpdateTagInput input)
    : CommandBase<UpdateTagInput, TagDto>(input);
