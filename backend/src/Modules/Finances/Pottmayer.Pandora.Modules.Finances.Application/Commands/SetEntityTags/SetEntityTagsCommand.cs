using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetEntityTags;

public sealed record SetEntityTagsInput(
    Guid UserId,
    string EntityType,
    Guid EntityId,
    IReadOnlyList<Guid> TagIds);

public sealed class SetEntityTagsCommand(SetEntityTagsInput input)
    : CommandBase<SetEntityTagsInput, IReadOnlyList<TagDto>>(input);
