using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class NoteCaptureRepository(IDataContextAccessor accessor)
    : StandardRepository<NoteCapture, Guid>(accessor), INoteCaptureRepository
{
    public Task<Page?> FindInboxAsync(Guid userId, CancellationToken ct = default)
    {
        var captures = Queryable();
        return DbContext.Set<Page>().Where(page => page.UserId == userId && page.DeletedAt == null &&
                captures.Any(capture => capture.UserId == userId && capture.PageId == page.Id))
            .OrderByDescending(page => page.CreatedAt).ThenByDescending(page => page.Id).FirstOrDefaultAsync(ct);
    }
}
