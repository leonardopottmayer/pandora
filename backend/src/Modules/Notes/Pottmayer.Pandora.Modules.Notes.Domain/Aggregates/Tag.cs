using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A label owned by the user (nte005). Unlike the tags of other modules, this one is not authored
/// through a CRUD screen: it comes into existence because a page's markdown mentions <c>#it</c>, and
/// the text stays in charge of which pages carry it (see <c>PageTag</c>).
///
/// What the row adds on top of the text is <see cref="Color"/> — the one thing a markdown file cannot
/// remember. That also makes it the reason to keep a tag no page mentions anymore: colored tags
/// survive going empty, plain ones are swept away.
///
/// <see cref="Slug"/> is the identity (unique per user); <see cref="Name"/> is only how it was first
/// written. Renaming is not an operation here — it would mean rewriting every page that mentions it.
/// </summary>
public sealed class Tag : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Color { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Whether the tag holds anything worth surviving its last page.</summary>
    public bool HasUserMetadata => !string.IsNullOrWhiteSpace(Color);

    private Tag() { }

    /// <summary>
    /// Registers a tag seen in a page's markdown. The caller normalized <paramref name="name"/> into
    /// <paramref name="slug"/> with <see cref="TagName"/> and checked the user has no tag with it.
    /// </summary>
    public static Tag Create(Guid userId, string slug, string name, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Slug = slug,
            Name = name,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Sets (or clears, with <c>null</c>) the color the tag is drawn with.</summary>
    public void SetColor(string? color) =>
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
}
