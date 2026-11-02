using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.RestorePageVersion;

public sealed record RestorePageVersionInput(
    Guid UserId,
    Guid PageId,
    long Number,
    long ExpectedVersion);

/// <summary>Restores a complete page state as a new version, retaining all previous versions.</summary>
public sealed class RestorePageVersionCommand(RestorePageVersionInput input)
    : CommandBase<RestorePageVersionInput, PageDto>(input);
