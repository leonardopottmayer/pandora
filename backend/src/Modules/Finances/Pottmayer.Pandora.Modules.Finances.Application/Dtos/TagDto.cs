using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record TagDto(Guid Id, string Name, string? Color)
{
    public static TagDto From(Tag t) => new(t.Id, t.Name, t.Color);
}
