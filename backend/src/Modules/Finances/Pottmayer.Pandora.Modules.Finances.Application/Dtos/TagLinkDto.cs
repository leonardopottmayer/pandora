using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record TagLinkDto(Guid Id, Guid TagId, string EntityType, Guid EntityId)
{
    public static TagLinkDto From(TagLink l) => new(l.Id, l.TagId, l.EntityType.Value, l.EntityId);
}
