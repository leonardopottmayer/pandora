using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// User-defined category (fin003). Hierarchical, max 2 levels (a child cannot have children); a
/// child's <see cref="Nature"/> equals its parent's. Nature and parent are fixed at creation;
/// renaming, re-colouring, reordering and (de)activation are the only mutations. Deactivation is
/// non-destructive — it keeps the row so existing transactions stay intact, hiding it from new use.
/// </summary>
public sealed class UserCategory : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TransactionNature Nature { get; private set; } = TransactionNature.Expense;
    public Guid? ParentCategoryId { get; private set; }
    public string? Color { get; private set; }
    public string? Icon { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsRoot => ParentCategoryId is null;

    private UserCategory() { }

    /// <summary>
    /// Builds a category. Cross-aggregate rules (parent exists and is itself a root, nature matches
    /// the parent, name is unique within the parent) are enforced by the caller before this runs.
    /// </summary>
    public static UserCategory Create(
        Guid userId,
        string name,
        TransactionNature nature,
        Guid? parentCategoryId,
        string? color,
        string? icon,
        int displayOrder,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Nature = nature,
            ParentCategoryId = parentCategoryId,
            Color = color,
            Icon = icon,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow()
        };

    public void Update(string name, string? color, string? icon, int displayOrder)
    {
        Name = name.Trim();
        Color = color;
        Icon = icon;
        DisplayOrder = displayOrder;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
