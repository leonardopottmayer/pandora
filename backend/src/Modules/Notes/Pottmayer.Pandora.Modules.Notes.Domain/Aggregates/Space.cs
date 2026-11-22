using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A group of pages (nte007), like a Confluence space: every <see cref="Page"/> belongs to one, and the
/// sidebar shows one space's tree at a time. Tags and wikilinks are not scoped by it.
///
/// <see cref="IsDefault"/> marks where a page goes when nothing says which space — the assistant, or
/// an API call without one. A user has at most one, and it can be neither archived nor deleted while
/// it is the default. Deleting is soft and only allowed once the space holds no live page, so a page
/// never points at a missing space.
/// </summary>
public sealed class Space : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public string? Color { get; private set; }
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    private Space() { }

    public static Space Create(
        Guid userId, string name, string? icon, string? color, string? description, bool isDefault,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Icon = Blank(icon),
            Color = Blank(color),
            Description = Blank(description),
            IsDefault = isDefault,
            CreatedAt = timeProvider.GetUtcNow()
        };

    public void Update(string name, string? icon, string? color, string? description)
    {
        Name = name.Trim();
        Icon = Blank(icon);
        Color = Blank(color);
        Description = Blank(description);
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void Archive(TimeProvider timeProvider) => ArchivedAt ??= timeProvider.GetUtcNow();

    public void Unarchive() => ArchivedAt = null;

    public void Delete(TimeProvider timeProvider) => DeletedAt ??= timeProvider.GetUtcNow();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
