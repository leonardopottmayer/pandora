using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// System category (fin002): global, seed-maintained reference data, hierarchical (2 levels). Root
/// of its own table, retrieved through its own reader — an aggregate root, just an immutable one:
/// read-only at runtime, written only by migration, no behaviour. Mirrors <see cref="UserCategory"/>.
/// </summary>
public sealed class SystemCategory : AggregateRoot<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TransactionNature Nature { get; private set; } = TransactionNature.Expense;
    public Guid? ParentCategoryId { get; private set; }
    public string? Color { get; private set; }
    public string? Icon { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsOther { get; private set; }
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }

    private SystemCategory() { }
}
