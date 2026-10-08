using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.UpdateRoot;

/// <summary>Replaces the root's settings. The device and path never change: that is a new root.</summary>
public sealed record UpdateRootInput(
    Guid UserId,
    Guid RootId,
    string? Name,
    TimeOnly? ScanTime,
    bool IncludeHidden,
    bool CaseSensitive);

public sealed class UpdateRootCommand(UpdateRootInput input)
    : CommandBase<UpdateRootInput, RootDto>(input);
