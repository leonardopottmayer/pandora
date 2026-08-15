using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetTagColor;

/// <summary><paramref name="Color"/> null (or blank) clears it, which also makes the tag sweepable again.</summary>
public sealed record SetTagColorInput(Guid UserId, Guid TagId, string? Color);

/// <summary>
/// The only thing that can be edited on a tag. The name comes from the text and renaming would mean
/// rewriting every page that mentions it — that is a find &amp; replace, not a command.
/// </summary>
public sealed class SetTagColorCommand(SetTagColorInput input)
    : CommandBase<SetTagColorInput, TagDto>(input);
