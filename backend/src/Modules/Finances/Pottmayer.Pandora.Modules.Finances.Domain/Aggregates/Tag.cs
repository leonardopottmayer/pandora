using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A free-form label owned by the user (fin004). The <see cref="Name"/> is unique per user; renaming
/// and re-colouring are the only mutations. Tags attach to any entity in the module through
/// <see cref="TagLink"/> (a separate aggregate) rather than a navigation here.
/// </summary>
public sealed class Tag : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Color { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Tag() { }

    /// <summary>
    /// Builds a tag. Name uniqueness within the user is enforced by the caller before this runs.
    /// </summary>
    public static Tag Create(Guid userId, string name, string? color, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Color = color,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Renames and/or recolors the tag. Uniqueness within the user is enforced by the caller.</summary>
    public void Update(string name, string? color)
    {
        Name = name.Trim();
        Color = color;
    }
}
