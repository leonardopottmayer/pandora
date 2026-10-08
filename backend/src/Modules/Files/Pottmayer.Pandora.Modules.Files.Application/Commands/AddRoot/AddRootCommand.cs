using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.AddRoot;

/// <param name="ScanTime">Daily, in the device's local time; null scans only on demand.</param>
/// <param name="CaseSensitive">Null takes the device platform's default.</param>
public sealed record AddRootInput(
    Guid UserId,
    Guid DeviceId,
    string? Name,
    string? LocalPath,
    TimeOnly? ScanTime,
    bool IncludeHidden,
    bool? CaseSensitive);

public sealed class AddRootCommand(AddRootInput input)
    : CommandBase<AddRootInput, RootDto>(input);
