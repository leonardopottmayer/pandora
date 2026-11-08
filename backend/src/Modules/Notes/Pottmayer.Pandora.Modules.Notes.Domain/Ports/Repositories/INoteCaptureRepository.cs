using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface INoteCaptureRepository : IStandardRepository<NoteCapture, Guid>
{
    /// <summary>The newest live page previously used for captures; names and locations may have changed.</summary>
    Task<Page?> FindInboxAsync(Guid userId, CancellationToken ct = default);
}
