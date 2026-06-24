using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTag;

public sealed record CreateTagInput(Guid UserId, string Name, string? Color);

/// <summary>Creates a tag the user can attach to any taggable entity (e.g. transactions, plans).</summary>
public sealed class CreateTagCommand(CreateTagInput input)
    : CommandBase<CreateTagInput, TagDto>(input);
