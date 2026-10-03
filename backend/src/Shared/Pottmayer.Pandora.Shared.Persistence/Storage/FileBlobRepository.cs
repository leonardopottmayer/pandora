using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Shared.Persistence.Storage;

/// <summary>
/// One repository for every module's blob table: it works on whichever data context acquired it, so
/// <c>notes</c> reads <c>nte003</c> and <c>finances</c> reads <c>fin018</c>.
/// </summary>
internal sealed class FileBlobRepository(IDataContextAccessor accessor)
    : StandardRepository<FileBlob, Guid>(accessor), IFileBlobRepository;
