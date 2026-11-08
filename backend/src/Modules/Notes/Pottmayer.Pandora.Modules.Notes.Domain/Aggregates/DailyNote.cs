using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>Connects a user's calendar day to a page, independently of its title and location.</summary>
public sealed class DailyNote : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public Guid PageId { get; private set; }
    private DailyNote() { }
    public static DailyNote Create(Guid userId, DateOnly date, Guid pageId) =>
        new() { Id = Guid.CreateVersion7(), UserId = userId, Date = date, PageId = pageId };
    public void ReplacePage(Guid pageId) => PageId = pageId;
}
