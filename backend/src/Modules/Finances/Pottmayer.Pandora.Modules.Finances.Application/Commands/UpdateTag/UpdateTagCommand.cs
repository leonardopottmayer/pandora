using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTag;

public sealed record UpdateTagInput(Guid UserId, Guid TagId, string Name, string? Color);

/// <summary>Renames a tag and/or changes its color. Applies retroactively everywhere it's already linked.</summary>
public sealed class UpdateTagCommand(UpdateTagInput input)
    : CommandBase<UpdateTagInput, TagDto>(input);
