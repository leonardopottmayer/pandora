using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTag;

public sealed record CreateTagInput(Guid UserId, string Name, string? Color);

public sealed class CreateTagCommand(CreateTagInput input)
    : CommandBase<CreateTagInput, TagDto>(input);
