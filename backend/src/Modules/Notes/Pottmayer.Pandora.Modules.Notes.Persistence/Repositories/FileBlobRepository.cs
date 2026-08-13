using Pottmayer.Pandora.Modules.Notes.Persistence.Storage;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class FileBlobRepository(IDataContextAccessor accessor)
    : StandardRepository<FileBlob, Guid>(accessor), IFileBlobRepository;
