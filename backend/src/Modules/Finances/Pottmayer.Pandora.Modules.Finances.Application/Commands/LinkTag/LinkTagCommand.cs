using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkTag;

public sealed record LinkTagInput(Guid UserId, Guid TagId, string EntityType, Guid EntityId);

public sealed class LinkTagCommand(LinkTagInput input)
    : CommandBase<LinkTagInput, TagLinkDto>(input);
